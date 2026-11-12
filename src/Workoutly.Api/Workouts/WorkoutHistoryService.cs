using System.Data;
using Npgsql;
using Workoutly.Api.Data;

namespace Workoutly.Api.Workouts;

public sealed record WorkoutDay(DateOnly Date, int SessionCount);
public sealed record LoadPoint(string SessionId, DateTimeOffset StartedAt, decimal MaxWeightKg, int SetCount, long RepetitionCount, decimal VolumeKg);
public sealed record ExerciseEvolution(string ExerciseId, string Name, List<LoadPoint> Points);
public sealed record WorkoutHistory(DateOnly From, DateOnly To, int OffsetMinutes, int SessionCount, int ActiveDays,
    int SetCount, long RepetitionCount, long DurationSeconds, decimal VolumeKg,
    List<WorkoutDay> Days, List<ExerciseEvolution> Exercises, List<WorkoutSummary> Sessions);

public sealed class WorkoutHistoryService(PostgresDatabase database)
{
    public async Task<WorkoutHistory?> GetAsync(string student, DateOnly from, DateOnly to, int offsetMinutes, CancellationToken ct, string? teacher = null)
    {
        var offset = TimeSpan.FromMinutes(offsetMinutes);
        var start = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), offset).UtcDateTime;
        var until = new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue), offset).UtcDateTime;
        await using var connection = await database.OpenConnectionAsync(ct);
        // Uma fotografia consistente para lista, totais e evolução, mesmo durante correções.
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        if (teacher is not null && !await LockEnrollment(connection, transaction, student, teacher, ct)) return null;
        var sessions = await WorkoutService.List(connection, transaction, student, start, until, ct);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT s.id,s.started_at,x.exercise_id,i.name,MAX(x.weight_kg),COUNT(*),SUM(x.repetitions),SUM(x.repetitions*x.weight_kg)
            FROM workout_sessions s JOIN workout_session_sets x ON x.session_id=s.id
            JOIN workout_plan_items i ON i.plan_id=s.plan_id AND i.version=s.plan_version AND i.exercise_id=x.exercise_id
            WHERE s.student_id=@student AND s.started_at>=@from AND s.started_at<@until
            GROUP BY s.id,s.started_at,x.exercise_id,i.name ORDER BY s.started_at,s.id,x.exercise_id
            """;
        command.Parameters.AddUuid("@student", student);
        command.Parameters.AddWithValue("@from", start);
        command.Parameters.AddWithValue("@until", until);
        var exercises = new Dictionary<string, ExerciseEvolution>();
        long repetitions = 0;
        await using (var reader = await command.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                var id = reader.GetGuid(2).ToString();
                var name = reader.GetString(3);
                if (!exercises.TryGetValue(id, out var exercise)) exercise = new(id, name, []);
                // O seletor usa o último nome executado; os detalhes preservam cada revisão.
                exercises[id] = exercise with { Name = name };
                var count = reader.GetInt64(6);
                repetitions += count;
                exercise.Points.Add(new(reader.GetGuid(0).ToString(), new(DateTime.SpecifyKind(reader.GetDateTime(1), DateTimeKind.Utc)), reader.GetDecimal(4), checked((int)reader.GetInt64(5)), count, reader.GetDecimal(7)));
            }
        }
        var days = sessions.GroupBy(s => DateOnly.FromDateTime(s.StartedAt.ToOffset(offset).DateTime))
            .OrderBy(g => g.Key).Select(g => new WorkoutDay(g.Key, g.Count())).ToList();
        await transaction.CommitAsync(ct);
        return new(from, to, offsetMinutes, sessions.Count, days.Count, sessions.Sum(s => s.SetCount), repetitions,
            sessions.Sum(s => (long)s.DurationSeconds), sessions.Sum(s => s.VolumeKg), days,
            exercises.Values.OrderBy(e => e.Name, StringComparer.Ordinal).ThenBy(e => e.ExerciseId).ToList(), sessions);
    }

    public async Task<WorkoutSession?> GetStudentSessionAsync(string teacher, string student, string session, CancellationToken ct)
    {
        await using var connection = await database.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        if (!await LockEnrollment(connection, transaction, student, teacher, ct)) return null;
        var result = await WorkoutService.Read(connection, transaction, student, session, ct);
        await transaction.CommitAsync(ct);
        return result;
    }

    private static async Task<bool> LockEnrollment(NpgsqlConnection connection, NpgsqlTransaction transaction, string student, string teacher, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        // A remoção do vínculo aguarda esta leitura; requisições seguintes já não terão acesso.
        command.CommandText = "SELECT student_id FROM student_teachers WHERE student_id=@student AND teacher_id=@teacher FOR SHARE";
        command.Parameters.AddUuid("@student", student);
        command.Parameters.AddUuid("@teacher", teacher);
        return await command.ExecuteScalarAsync(ct) is not null;
    }
}
