using System.Security.Claims;
namespace Workoutly.Api.Workouts;

public static class WorkoutEndpoints
{
    public static void MapWorkoutEndpoints(this WebApplication app)
    {
        var sessions = app.MapGroup("/sessions").RequireAuthorization("student");
        sessions.MapGet("/", async (ClaimsPrincipal user, WorkoutService service, CancellationToken ct) =>
            Results.Ok(await service.ListAsync(user.FindFirstValue(ClaimTypes.NameIdentifier)!, ct)));
        sessions.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal user, WorkoutService service, CancellationToken ct) =>
        {
            var session = await service.GetAsync(user.FindFirstValue(ClaimTypes.NameIdentifier)!, id.ToString(), ct);
            return session is null ? Results.NotFound() : Results.Ok(session);
        });
        sessions.MapPost("/", async (CreateWorkoutRequest request, ClaimsPrincipal user, WorkoutService service, TimeProvider time, CancellationToken ct) =>
        {
            var errors = Validate(request, time.GetUtcNow());
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            var result = await service.CreateAsync(user.FindFirstValue(ClaimTypes.NameIdentifier)!, request, ct);
            return result.Status switch
            {
                "created" => Results.Created($"/sessions/{result.Session!.Id}", result.Session),
                "existing" => Results.Ok(result.Session),
                "conflict" => Results.Conflict(new { message = "Esta tentativa já corresponde a outro conteúdo salvo. Consulte os treinos salvos antes de registrar novamente." }),
                "exercise_invalid" => Results.ValidationProblem(new Dictionary<string, string[]> { ["exercises"] = ["Registre apenas exercícios presentes na revisão da ficha executada."] }),
                _ => Results.NotFound()
            };
        });
        sessions.MapPut("/{id:guid}", async (Guid id, UpdateWorkoutRequest request, ClaimsPrincipal user, WorkoutService service, TimeProvider time, CancellationToken ct) =>
        {
            var errors = ValidateValues(request.DurationSeconds, request.Notes, request.Exercises);
            if (request.EditId == Guid.Empty || request.Version < 1 || request.Version == int.MaxValue)
                errors["version"] = ["Informe um identificador da correção e a versão atual do registro."];
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            var result = await service.UpdateAsync(user.FindFirstValue(ClaimTypes.NameIdentifier)!, id.ToString(), request, time.GetUtcNow(), ct);
            return result.Status switch
            {
                "updated" or "existing" => Results.Ok(result.Session),
                "conflict" => Results.Conflict(new { message = "O treino já foi alterado. Recarregue a versão atual antes de corrigir novamente." }),
                "time_invalid" => Results.ValidationProblem(new Dictionary<string, string[]> { ["durationSeconds"] = ["A duração informada faria o treino terminar no futuro. Revise o valor."] }),
                "exercise_invalid" => Results.ValidationProblem(new Dictionary<string, string[]> { ["exercises"] = ["Use somente exercícios da ficha original deste treino."] }),
                _ => Results.NotFound()
            };
        });
    }
    private static Dictionary<string, string[]> Validate(CreateWorkoutRequest request, DateTimeOffset now)
    {
        var errors = ValidateValues(request.DurationSeconds, request.Notes, request.Exercises);
        if (request.Id == Guid.Empty || request.PlanId == Guid.Empty || request.PlanVersion < 1)
            errors["plan"] = ["Informe identificadores válidos e a revisão da ficha executada."];
        if (request.StartedAt < new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero) || request.StartedAt > now.AddMinutes(5) ||
            request.StartedAt.ToUnixTimeSeconds() + (long)request.DurationSeconds > now.AddMinutes(5).ToUnixTimeSeconds())
            errors["startedAt"] = ["Confira a data e a hora do aparelho; o treino não pode terminar no futuro."];
        return errors;
    }
    private static Dictionary<string, string[]> ValidateValues(int duration, string? notes, PerformedExercise[]? exercises)
    {
        var errors = new Dictionary<string, string[]>();
        if (duration is < 1 or > 86400) errors["durationSeconds"] = ["A duração deve estar entre 1 segundo e 24 horas."];
        if (notes?.Trim().Length > 1000) errors["notes"] = ["Use até 1000 caracteres nas observações."];
        if (exercises is null || exercises.Length is < 1 or > 50 || exercises.Any(e => e is null || e.ExerciseId == Guid.Empty || e.Sets is null || e.Sets.Length is < 1 or > 20 ||
            e.Sets.Any(s => s is null || s.Repetitions is < 1 or > 200 || s.WeightKg is < 0 or > 2000 || decimal.Round(s.WeightKg, 2) != s.WeightKg)) ||
            exercises.Select(e => e.ExerciseId).Distinct().Count() != exercises.Length)
            errors["exercises"] = ["Registre de 1 a 50 exercícios distintos, com 1 a 20 séries realizadas, 1 a 200 repetições e carga de 0 a 2000 kg com até duas casas decimais. Use 0 kg para peso corporal."];
        return errors;
    }
}
