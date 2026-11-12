using Workoutly.Api.Auth;
using Workoutly.Api.Data;
using Npgsql;
using System.Security.Cryptography;

namespace Workoutly.Api.Teaching;

public sealed record TeacherInvitation(string? Code, DateTimeOffset? ExpiresAt);
public sealed record Enrollment(AccountProfile? Teacher);

public sealed class TeachingService(PostgresDatabase database, TimeProvider time)
{
    public async Task<List<AccountProfile>> ListStudentsAsync(string teacherId, CancellationToken cancellationToken, string? studentId = null)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT u.id, u.name, u.email, r.role FROM student_teachers e
            JOIN users u ON u.id = e.student_id JOIN account_roles r ON r.user_id = u.id
            WHERE e.teacher_id = @teacher AND r.role = 'student' AND (@student IS NULL OR e.student_id = @student) ORDER BY u.name, u.id
            """;
        command.Parameters.AddUuid("teacher", teacherId);
        command.Parameters.AddUuid("student", studentId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var students = new List<AccountProfile>();
        while (await reader.ReadAsync(cancellationToken))
            students.Add(new(reader.GetGuid(0).ToString(), reader.GetString(1), reader.GetString(2), reader.GetString(3)));
        return students;
    }

    public async Task<TeacherInvitation> GetInvitationAsync(string teacherId, CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT code, expires_at FROM teacher_invitations WHERE teacher_id = @teacher AND expires_at > @now";
        command.Parameters.AddUuid("teacher", teacherId);
        command.Parameters.AddWithValue("now", time.GetUtcNow().UtcDateTime);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new(reader.GetString(0), new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime(1), DateTimeKind.Utc)))
            : new(null, null);
    }

    public async Task<TeacherInvitation> CreateInvitationAsync(string teacherId, CancellationToken cancellationToken)
    {
        var invitation = new TeacherInvitation(Convert.ToHexString(RandomNumberGenerator.GetBytes(6)),
            DateTimeOffset.FromUnixTimeSeconds(time.GetUtcNow().AddDays(7).ToUnixTimeSeconds()));
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        // Serializa renovações da mesma conta e mantém o código anterior se a gravação falhar.
        command.CommandText = "SELECT id FROM users WHERE id = @teacher FOR UPDATE";
        command.Parameters.AddUuid("teacher", teacherId);
        await command.ExecuteScalarAsync(cancellationToken);
        command.CommandText = "DELETE FROM teacher_invitations WHERE teacher_id = @teacher";
        await command.ExecuteNonQueryAsync(cancellationToken);
        command.CommandText = "INSERT INTO teacher_invitations (teacher_id, code, expires_at) VALUES (@teacher, @code, @expires)";
        command.Parameters.AddWithValue("code", invitation.Code!);
        command.Parameters.AddWithValue("expires", invitation.ExpiresAt!.Value.UtcDateTime);
        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return invitation;
    }

    public async Task<Enrollment> GetEnrollmentAsync(string studentId, CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT u.id, u.name, u.email, r.role FROM student_teachers e
            JOIN users u ON u.id = e.teacher_id JOIN account_roles r ON r.user_id = u.id
            WHERE e.student_id = @student AND r.role = 'teacher'
            """;
        command.Parameters.AddUuid("student", studentId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return new(await reader.ReadAsync(cancellationToken)
            ? new(reader.GetGuid(0).ToString(), reader.GetString(1), reader.GetString(2), reader.GetString(3)) : null);
    }

    public async Task<(string Result, Enrollment Enrollment)> JoinAsync(string studentId, string code, CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO student_teachers (student_id, teacher_id)
            SELECT @student, i.teacher_id FROM teacher_invitations i JOIN account_roles r ON r.user_id = i.teacher_id
            WHERE i.code = @code AND i.expires_at > @now AND r.role = 'teacher'
            """;
        command.Parameters.AddUuid("student", studentId);
        command.Parameters.AddWithValue("code", code);
        command.Parameters.AddWithValue("now", time.GetUtcNow().UtcDateTime);
        try
        {
            var created = await command.ExecuteNonQueryAsync(cancellationToken);
            return created == 0 ? ("invalid", new(null)) : ("created", await GetEnrollmentAsync(studentId, cancellationToken));
        }
        catch (NpgsqlException exception) when (exception is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            var current = await GetEnrollmentAsync(studentId, cancellationToken);
            command.CommandText = "SELECT teacher_id FROM teacher_invitations WHERE code = @code AND expires_at > @now";
            var teacher = await command.ExecuteScalarAsync(cancellationToken);
            return (current.Teacher is not null && current.Teacher.Id == teacher?.ToString() ? "existing" : "conflict", current);
        }
    }

    public async Task<bool> RemoveStudentAsync(string teacherId, string studentId, CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM student_teachers WHERE teacher_id = @teacher AND student_id = @student";
        command.Parameters.AddUuid("teacher", teacherId);
        command.Parameters.AddUuid("student", studentId);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    public async Task LeaveAsync(string studentId, CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM student_teachers WHERE student_id = @student";
        command.Parameters.AddUuid("student", studentId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
