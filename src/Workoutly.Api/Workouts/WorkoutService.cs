using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Npgsql;
using Workoutly.Api.Data;
using Workoutly.Api.Plans;
using System.Text;

namespace Workoutly.Api.Workouts;

public sealed record PerformedSet(int Repetitions, decimal WeightKg);
public sealed record PerformedExercise(Guid ExerciseId, PerformedSet[]? Sets);
public sealed record CreateWorkoutRequest(Guid Id, Guid PlanId, int PlanVersion, DateTimeOffset StartedAt, int DurationSeconds, string? Notes, PerformedExercise[]? Exercises);
public sealed record UpdateWorkoutRequest(Guid EditId, int Version, int DurationSeconds, string? Notes, PerformedExercise[]? Exercises);
public sealed record WorkoutExercise(string ExerciseId, string Name, string MuscleGroup, string Equipment, string Description, int PlannedSets, int PlannedRepetitions, List<PerformedSet> Sets);
public sealed record WorkoutSession(string Id, string StudentId, string PlanId, int PlanVersion, string PlanName, DateTimeOffset StartedAt, int DurationSeconds, string Notes, List<WorkoutExercise> Exercises, int Version, List<PlanExercise> PlanExercises)
{
    public decimal VolumeKg => Exercises.Sum(exercise => exercise.Sets.Sum(set => set.Repetitions * set.WeightKg));
}
public sealed record WorkoutSummary(string Id, string PlanName, DateTimeOffset StartedAt, int DurationSeconds, int ExerciseCount, int SetCount, decimal VolumeKg);
public sealed record WorkoutResult(string Status, WorkoutSession? Session = null);

public sealed class WorkoutService(PostgresDatabase database)
{
    public async Task<List<WorkoutSummary>> ListAsync(string student, CancellationToken ct)
    {
        await using var connection = await database.OpenConnectionAsync(ct);
        return await List(connection, null, student, null, null, ct);
    }

    internal static async Task<List<WorkoutSummary>> List(NpgsqlConnection connection, NpgsqlTransaction? transaction, string student, DateTime? from, DateTime? until, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT s.id,v.name,s.started_at,s.duration_seconds,COUNT(DISTINCT x.exercise_id),COUNT(*),SUM(x.repetitions*x.weight_kg)
            FROM workout_sessions s
            JOIN workout_plan_versions v ON v.plan_id=s.plan_id AND v.version=s.plan_version
            JOIN workout_session_sets x ON x.session_id=s.id
            WHERE s.student_id=@student AND (@from IS NULL OR s.started_at>=@from) AND (@until IS NULL OR s.started_at<@until)
            GROUP BY s.id,v.name,s.started_at,s.duration_seconds
            ORDER BY s.started_at DESC,s.id
            """;
        command.Parameters.AddUuid("@student", student);
        command.Parameters.AddWithValue("@from", NpgsqlTypes.NpgsqlDbType.TimestampTz, (object?)from ?? DBNull.Value);
        command.Parameters.AddWithValue("@until", NpgsqlTypes.NpgsqlDbType.TimestampTz, (object?)until ?? DBNull.Value);
        var sessions = new List<WorkoutSummary>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            sessions.Add(new(reader.GetGuid(0).ToString(), reader.GetString(1), Utc(reader.GetDateTime(2)), reader.GetInt32(3), checked((int)reader.GetInt64(4)), checked((int)reader.GetInt64(5)), reader.GetDecimal(6)));
        return sessions;
    }

    public async Task<WorkoutSession?> GetAsync(string student, string id, CancellationToken ct)
    {
        await using var connection = await database.OpenConnectionAsync(ct);
        return await Read(connection, null, student, id, ct);
    }
    private static DateTimeOffset Utc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
    internal static async Task<WorkoutSession?> Read(NpgsqlConnection connection, NpgsqlTransaction? transaction, string student, string id, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT s.id,s.student_id,s.plan_id,s.plan_version,v.name,s.started_at,s.duration_seconds,s.notes,
                   i.exercise_id,i.name,i.muscle_group,i.equipment,i.description,i.set_count,i.repetitions,x.repetitions,x.weight_kg,
                   COALESCE((SELECT MAX(c.version) FROM workout_session_changes c WHERE c.session_id=s.id),1)
            FROM workout_sessions s
            JOIN workout_plan_versions v ON v.plan_id=s.plan_id AND v.version=s.plan_version
            JOIN workout_session_sets x ON x.session_id=s.id
            JOIN workout_plan_items i ON i.plan_id=s.plan_id AND i.version=s.plan_version AND i.exercise_id=x.exercise_id
            WHERE s.student_id=@student AND s.id=@id
            ORDER BY i.position,x.set_position
            """;
        command.Parameters.AddUuid("@student", student);
        command.Parameters.AddUuid("@id", id);
        WorkoutSession? session = null;
        await using (var reader = await command.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                session ??= new(reader.GetGuid(0).ToString(), reader.GetGuid(1).ToString(), reader.GetGuid(2).ToString(), reader.GetInt32(3), reader.GetString(4), Utc(reader.GetDateTime(5)), reader.GetInt32(6), reader.GetString(7), [], reader.GetInt32(17), []);
                var exerciseId = reader.GetGuid(8).ToString();
                var exercise = session.Exercises.LastOrDefault();
                if (exercise?.ExerciseId != exerciseId)
                {
                    exercise = new(exerciseId, reader.GetString(9), reader.GetString(10), reader.GetString(11), reader.GetString(12), reader.GetInt32(13), reader.GetInt32(14), []);
                    session.Exercises.Add(exercise);
                }
                exercise.Sets.Add(new(reader.GetInt32(15), reader.GetDecimal(16)));
            }
        }
        if (session is not null)
        {
            command.CommandText = "SELECT exercise_id,name,muscle_group,equipment,description,set_count,repetitions FROM workout_plan_items WHERE plan_id=@plan AND version=@version ORDER BY position";
            command.Parameters.AddUuid("@plan", session.PlanId);
            command.Parameters.AddWithValue("@version", session.PlanVersion);
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                session.PlanExercises.Add(new(reader.GetGuid(0).ToString(), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetInt32(5), reader.GetInt32(6)));
        }
        return session;
    }

    // Canonicalização: ordem do corpo, fuso e casas decimais equivalentes não criam outro registro.
    private static string Fingerprint(CreateWorkoutRequest request) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
    {
        request.PlanId, request.PlanVersion, StartedAt = request.StartedAt.ToUnixTimeMilliseconds(), request.DurationSeconds,
        Notes = request.Notes?.Trim() ?? string.Empty,
        Exercises = request.Exercises!.OrderBy(e => e.ExerciseId).Select(e => new
        {
            e.ExerciseId, Sets = e.Sets!.Select(s => new { s.Repetitions, WeightKg = s.WeightKg.ToString("0.##", CultureInfo.InvariantCulture) })
        })
    })));

    public async Task<WorkoutResult> CreateAsync(string student, CreateWorkoutRequest request, CancellationToken ct)
    {
        var id = request.Id.ToString();
        var fingerprint = Fingerprint(request);
        await using var connection = await database.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        // Mesmo bloqueio das fichas: serializa criação, edição da ficha e reenvios desta conta.
        command.CommandText = "SELECT user_id FROM account_roles WHERE user_id=@student AND role='student' FOR UPDATE";
        command.Parameters.AddUuid("@student", student);
        if (await command.ExecuteScalarAsync(ct) is null) return new("missing");
        command.Parameters.AddUuid("@id", id);
        command.CommandText = "SELECT student_id,request_hash FROM workout_sessions WHERE id=@id";
        bool exists;
        bool sameRequest;
        await using (var reader = await command.ExecuteReaderAsync(ct))
        {
            exists = await reader.ReadAsync(ct);
            sameRequest = exists && reader.GetGuid(0).ToString() == student && reader.GetString(1) == fingerprint;
        }
        if (exists) return sameRequest ? new("existing", await Read(connection, transaction, student, id, ct)) : new("conflict");
        command.CommandText = """
            SELECT i.exercise_id FROM workout_plans p
            JOIN workout_plan_items i ON i.plan_id=p.id AND i.version=@version
            WHERE p.id=@plan AND p.student_id=@student
            """;
        command.Parameters.AddUuid("@plan", request.PlanId.ToString());
        command.Parameters.AddWithValue("@version", request.PlanVersion);
        var allowed = new HashSet<Guid>();
        await using (var reader = await command.ExecuteReaderAsync(ct))
            while (await reader.ReadAsync(ct)) allowed.Add(reader.GetGuid(0));
        if (allowed.Count == 0) return new("missing");
        if (request.Exercises!.Any(item => !allowed.Contains(item.ExerciseId))) return new("exercise_invalid");
        command.CommandText = """
            INSERT INTO workout_sessions (id,student_id,plan_id,plan_version,started_at,duration_seconds,notes,request_hash)
            VALUES (@id,@student,@plan,@version,@started,@duration,@notes,@hash)
            """;
        command.Parameters.AddWithValue("@started", DateTimeOffset.FromUnixTimeMilliseconds(request.StartedAt.ToUnixTimeMilliseconds()).UtcDateTime);
        command.Parameters.AddWithValue("@duration", request.DurationSeconds);
        command.Parameters.AddWithValue("@notes", request.Notes?.Trim() ?? string.Empty);
        command.Parameters.AddWithValue("@hash", fingerprint);
        try { await command.ExecuteNonQueryAsync(ct); }
        catch (NpgsqlException exception) when (exception is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }) { return new("conflict"); }
        await InsertSets(connection, transaction, id, request.PlanId.ToString(), request.PlanVersion, request.Exercises!, ct);
        await InsertChange(connection, transaction, id, 1, id, fingerprint, ct);
        var session = await Read(connection, transaction, student, id, ct);
        await transaction.CommitAsync(ct);
        return new("created", session);
    }

    public async Task<WorkoutResult> UpdateAsync(string student, string id, UpdateWorkoutRequest request, DateTimeOffset now, CancellationToken ct)
    {
        await using var connection = await database.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT user_id FROM account_roles WHERE user_id=@student AND role='student' FOR UPDATE";
        command.Parameters.AddUuid("@student", student);
        if (await command.ExecuteScalarAsync(ct) is null) return new("missing");
        var current = await Read(connection, transaction, student, id, ct);
        if (current is null) return new("missing");
        var canonical = new CreateWorkoutRequest(Guid.Parse(id), Guid.Parse(current.PlanId), current.PlanVersion,
            current.StartedAt, request.DurationSeconds, request.Notes, request.Exercises);
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.Version.ToString(CultureInfo.InvariantCulture) + ":" + Fingerprint(canonical))));
        command.CommandText = "SELECT session_id,request_hash FROM workout_session_changes WHERE operation_id=@operation";
        command.Parameters.AddUuid("@operation", request.EditId.ToString());
        await using (var reader = await command.ExecuteReaderAsync(ct))
        {
            if (await reader.ReadAsync(ct))
                return reader.GetGuid(0).ToString() == id && reader.GetString(1) == fingerprint ? new("existing", current) : new("conflict");
        }
        if (current.Version != request.Version) return new("conflict");
        if (current.StartedAt.ToUnixTimeSeconds() + (long)request.DurationSeconds > now.AddMinutes(5).ToUnixTimeSeconds()) return new("time_invalid");
        var allowed = current.PlanExercises.Select(e => e.ExerciseId).ToHashSet();
        if (request.Exercises!.Any(e => !allowed.Contains(e.ExerciseId.ToString()))) return new("exercise_invalid");
        command.Parameters.AddUuid("@id", id);
        command.CommandText = "DELETE FROM workout_session_sets WHERE session_id=@id";
        await command.ExecuteNonQueryAsync(ct);
        command.CommandText = "UPDATE workout_sessions SET duration_seconds=@duration,notes=@notes WHERE id=@id AND student_id=@student";
        command.Parameters.AddWithValue("@duration", request.DurationSeconds);
        command.Parameters.AddWithValue("@notes", request.Notes?.Trim() ?? string.Empty);
        await command.ExecuteNonQueryAsync(ct);
        await InsertSets(connection, transaction, id, current.PlanId, current.PlanVersion, request.Exercises!, ct);
        try { await InsertChange(connection, transaction, id, current.Version + 1, request.EditId.ToString(), fingerprint, ct); }
        catch (NpgsqlException exception) when (exception is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }) { return new("conflict"); }
        var updated = await Read(connection, transaction, student, id, ct);
        await transaction.CommitAsync(ct);
        return new("updated", updated);
    }

    private static async Task InsertChange(NpgsqlConnection connection, NpgsqlTransaction transaction, string id, int version, string operation, string hash, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "INSERT INTO workout_session_changes (session_id,version,operation_id,request_hash) VALUES (@id,@version,@operation,@hash)";
        command.Parameters.AddUuid("@id", id);
        command.Parameters.AddWithValue("@version", version);
        command.Parameters.AddUuid("@operation", operation);
        command.Parameters.AddWithValue("@hash", hash);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task InsertSets(NpgsqlConnection connection, NpgsqlTransaction transaction, string id, string plan, int version, PerformedExercise[] exercises, CancellationToken ct)
    {
        foreach (var exercise in exercises)
            for (var index = 0; index < exercise.Sets!.Length; index++)
            {
                var set = exercise.Sets[index];
                await using var insert = connection.CreateCommand();
                insert.Transaction = transaction;
                insert.CommandText = """
                    INSERT INTO workout_session_sets (session_id,plan_id,plan_version,exercise_id,set_position,repetitions,weight_kg)
                    VALUES (@id,@plan,@version,@exercise,@position,@repetitions,@weight)
                    """;
                insert.Parameters.AddUuid("@id", id);
                insert.Parameters.AddUuid("@plan", plan);
                insert.Parameters.AddWithValue("@version", version);
                insert.Parameters.AddUuid("@exercise", exercise.ExerciseId.ToString());
                insert.Parameters.AddWithValue("@position", index);
                insert.Parameters.AddWithValue("@repetitions", set.Repetitions);
                insert.Parameters.AddWithValue("@weight", set.WeightKg);
                await insert.ExecuteNonQueryAsync(ct);
            }
    }

}
