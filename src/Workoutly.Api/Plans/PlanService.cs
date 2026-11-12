using Npgsql;
using Workoutly.Api.Data;

namespace Workoutly.Api.Plans;

public sealed record PlanExercise(string ExerciseId, string Name, string MuscleGroup, string Equipment, string Description, int Sets, int Repetitions);
public sealed record WorkoutPlan(string Id, string StudentId, string Name, string Notes, int Version, List<PlanExercise> Exercises);
public sealed record PlanExerciseRequest(Guid ExerciseId, int Sets, int Repetitions);
public sealed record PlanResult(string Status, WorkoutPlan? Plan = null);

public sealed class PlanService(PostgresDatabase database)
{
    // O aluno é o proprietário. O professor recebe acesso apenas durante o vínculo ativo.
    private static async Task<bool> CanAccess(NpgsqlConnection connection, NpgsqlTransaction transaction,
        string actor, string student, bool teacher, CancellationToken ct)
    {
        if (!teacher && actor != student) return false;
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT user_id FROM account_roles WHERE user_id=@student AND role='student' FOR UPDATE";
        command.Parameters.AddUuid("@student", student);
        if (await command.ExecuteScalarAsync(ct) is null) return false;
        if (!teacher) return true;
        command.CommandText = "SELECT student_id FROM student_teachers WHERE student_id=@student AND teacher_id=@actor FOR UPDATE";
        command.Parameters.AddUuid("@actor", actor);
        return await command.ExecuteScalarAsync(ct) is not null;
    }

    public async Task<List<WorkoutPlan>?> ListAsync(string actor, string student, bool teacher, string? id, int? version, CancellationToken ct)
    {
        await using var connection = await database.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        if (!await CanAccess(connection, transaction, actor, student, teacher, ct)) return null;
        return await Read(connection, transaction, student, id, version, ct);
    }

    private static async Task<List<WorkoutPlan>> Read(NpgsqlConnection connection, NpgsqlTransaction transaction,
        string student, string? id, int? version, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT p.id, p.student_id, v.name, v.notes, v.version,
                   i.exercise_id, i.name, i.muscle_group, i.equipment, i.description, i.set_count, i.repetitions
            FROM workout_plans p
            JOIN workout_plan_versions v ON v.plan_id=p.id AND v.version=COALESCE(@version,p.current_version)
            JOIN workout_plan_items i ON i.plan_id=v.plan_id AND i.version=v.version
            WHERE p.student_id=@student AND p.deleted_at IS NULL AND (@id IS NULL OR p.id=@id)
            ORDER BY v.name, p.id, i.position
            """;
        command.Parameters.AddUuid("@student", student);
        command.Parameters.AddUuid("@id", id);
        command.Parameters.AddWithValue("@version", NpgsqlTypes.NpgsqlDbType.Integer, (object?)version ?? DBNull.Value);
        var plans = new List<WorkoutPlan>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var planId = reader.GetGuid(0).ToString();
            var plan = plans.LastOrDefault();
            if (plan?.Id != planId)
            {
                plan = new(planId, reader.GetGuid(1).ToString(), reader.GetString(2), reader.GetString(3), reader.GetInt32(4), []);
                plans.Add(plan);
            }
            plan.Exercises.Add(new(reader.GetGuid(5).ToString(), reader.GetString(6), reader.GetString(7),
                reader.GetString(8), reader.GetString(9), reader.GetInt32(10), reader.GetInt32(11)));
        }
        return plans;
    }

    public async Task<PlanResult> SaveAsync(string actor, string student, bool teacher, string id, int expectedVersion,
        string name, string? notes, PlanExerciseRequest[] items, CancellationToken ct)
    {
        name = name.Trim();
        notes = notes?.Trim() ?? string.Empty;
        await using var connection = await database.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        if (!await CanAccess(connection, transaction, actor, student, teacher, ct)) return new("missing");
        var current = (await Read(connection, transaction, student, id, null, ct)).SingleOrDefault();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.Parameters.AddUuid("@id", id);
        command.Parameters.AddUuid("@student", student);
        if (expectedVersion == 0)
        {
            // O mesmo ID de criação pode ser reenviado sem duplicar a ficha.
            if (current is not null)
                return current.Version == 1 && current.Name == name && current.Notes == notes &&
                    current.Exercises.Select(e => (e.ExerciseId, e.Sets, e.Repetitions)).SequenceEqual(items.Select(e => (e.ExerciseId.ToString(), e.Sets, e.Repetitions)))
                    ? new("existing", current) : new("conflict");
            command.CommandText = "SELECT id FROM workout_plans WHERE id=@id";
            if (await command.ExecuteScalarAsync(ct) is not null) return new("conflict");
        }
        else
        {
            if (current is null) return new("missing");
            if (current.Version != expectedVersion) return new("conflict");
        }
        var exercises = new List<PlanExercise>();
        foreach (var item in items)
        {
            var existing = current?.Exercises.FirstOrDefault(e => e.ExerciseId == item.ExerciseId.ToString());
            if (existing is not null)
            {
                exercises.Add(existing with { Sets = item.Sets, Repetitions = item.Repetitions });
                continue;
            }
            await using var lookup = connection.CreateCommand();
            lookup.Transaction = transaction;
            lookup.CommandText = "SELECT name,muscle_group,equipment,description FROM exercises WHERE id=@exercise AND (owner_id IS NULL OR owner_id=@actor)";
            lookup.Parameters.AddUuid("@exercise", item.ExerciseId.ToString());
            lookup.Parameters.AddUuid("@actor", actor);
            await using var reader = await lookup.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) return new("exercise_invalid");
            exercises.Add(new(item.ExerciseId.ToString(), reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), item.Sets, item.Repetitions));
        }
        var next = expectedVersion + 1;
        command.Parameters.AddWithValue("@version", next);
        command.CommandText = expectedVersion == 0
            ? "INSERT INTO workout_plans (id,student_id,current_version) VALUES (@id,@student,@version)"
            : "UPDATE workout_plans SET current_version=@version WHERE id=@id AND student_id=@student";
        try { await command.ExecuteNonQueryAsync(ct); }
        catch (NpgsqlException exception) when (exception is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }) { return new("conflict"); }
        command.CommandText = "INSERT INTO workout_plan_versions (plan_id,version,name,notes,author_id) VALUES (@id,@version,@name,@notes,@actor)";
        command.Parameters.AddWithValue("@name", name);
        command.Parameters.AddWithValue("@notes", notes);
        command.Parameters.AddUuid("@actor", actor);
        await command.ExecuteNonQueryAsync(ct);
        for (var position = 0; position < exercises.Count; position++)
        {
            var item = exercises[position];
            await using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = """
                INSERT INTO workout_plan_items (plan_id,version,position,exercise_id,name,muscle_group,equipment,description,set_count,repetitions)
                VALUES (@id,@version,@position,@exercise,@name,@muscle,@equipment,@description,@sets,@repetitions)
                """;
            insert.Parameters.AddUuid("@id", id);
            insert.Parameters.AddWithValue("@version", next);
            insert.Parameters.AddWithValue("@position", position);
            insert.Parameters.AddUuid("@exercise", item.ExerciseId);
            insert.Parameters.AddWithValue("@name", item.Name);
            insert.Parameters.AddWithValue("@muscle", item.MuscleGroup);
            insert.Parameters.AddWithValue("@equipment", item.Equipment);
            insert.Parameters.AddWithValue("@description", item.Description);
            insert.Parameters.AddWithValue("@sets", item.Sets);
            insert.Parameters.AddWithValue("@repetitions", item.Repetitions);
            await insert.ExecuteNonQueryAsync(ct);
        }
        await transaction.CommitAsync(ct);
        return new(expectedVersion == 0 ? "created" : "updated", new(id, student, name, notes, next, exercises));
    }

    public async Task<string> DeleteAsync(string actor, string student, bool teacher, string id, int version, CancellationToken ct)
    {
        await using var connection = await database.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        if (!await CanAccess(connection, transaction, actor, student, teacher, ct)) return "missing";
        var current = (await Read(connection, transaction, student, id, null, ct)).SingleOrDefault();
        if (current is null) return "missing";
        if (current.Version != version) return "conflict";
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "UPDATE workout_plans SET deleted_at=CURRENT_TIMESTAMP WHERE id=@id AND student_id=@student";
        command.Parameters.AddUuid("@id", id);
        command.Parameters.AddUuid("@student", student);
        await command.ExecuteNonQueryAsync(ct);
        await transaction.CommitAsync(ct);
        return "deleted";
    }
}
