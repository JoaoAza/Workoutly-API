using Npgsql;
using Workoutly.Api.Data;

namespace Workoutly.Api.Exercises;

public sealed record Exercise(string Id, string Name, string MuscleGroup, string Equipment, string Description, bool IsShared, bool IsOwned, bool IsInPlan);

public sealed class ExerciseService(PostgresDatabase database)
{
    public async Task<Exercise> CreateAsync(string ownerId, string name, string muscleGroup, string equipment, string? description, CancellationToken cancellationToken)
    {
        var exercise = new Exercise(Guid.NewGuid().ToString(), name.Trim(), muscleGroup.Trim(), equipment.Trim(), description?.Trim() ?? string.Empty, false, true, false);
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO exercises (id, owner_id, name, muscle_group, equipment, description)
            VALUES (@id, @owner, @name, @muscle, @equipment, @description)
            """;
        command.Parameters.AddUuid("@id", exercise.Id);
        command.Parameters.AddUuid("@owner", ownerId);
        command.Parameters.AddWithValue("@name", exercise.Name);
        command.Parameters.AddWithValue("@muscle", exercise.MuscleGroup);
        command.Parameters.AddWithValue("@equipment", exercise.Equipment);
        command.Parameters.AddWithValue("@description", exercise.Description);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return exercise;
    }

    public async Task<List<Exercise>> ListAsync(string ownerId, string? id, CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            WITH plan_items AS (
                SELECT i.*, ROW_NUMBER() OVER (PARTITION BY i.exercise_id ORDER BY v.created_at DESC, p.id) AS item_rank
                FROM workout_plans p
                JOIN workout_plan_versions v ON v.plan_id=p.id AND v.version=p.current_version
                JOIN workout_plan_items i ON i.plan_id=p.id AND i.version=p.current_version
                WHERE p.student_id=@owner AND p.deleted_at IS NULL
            ), catalog AS (
                SELECT e.id, e.name, e.muscle_group, e.equipment, e.description,
                       e.owner_id IS NULL AS is_shared, COALESCE(e.owner_id=@owner, FALSE) AS is_owned,
                       EXISTS(SELECT 1 FROM plan_items i WHERE i.exercise_id=e.id) AS is_in_plan
                FROM exercises e WHERE e.owner_id IS NULL OR e.owner_id=@owner
                UNION ALL
                SELECT i.exercise_id, i.name, i.muscle_group, i.equipment, i.description, FALSE, FALSE, TRUE
                FROM plan_items i JOIN exercises e ON e.id=i.exercise_id
                WHERE i.item_rank=1 AND e.owner_id IS NOT NULL AND e.owner_id<>@owner
            )
            SELECT * FROM catalog WHERE @id IS NULL OR id=@id
            ORDER BY name, id
            """;
        command.Parameters.AddUuid("@owner", ownerId);
        command.Parameters.AddUuid("@id", id);
        var exercises = new List<Exercise>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            exercises.Add(new(reader.GetGuid(0).ToString(), reader.GetString(1), reader.GetString(2),
                reader.GetString(3), reader.GetString(4), reader.GetBoolean(5), reader.GetBoolean(6), reader.GetBoolean(7)));
        return exercises;
    }
}
