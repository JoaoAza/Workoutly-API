using System.Security.Claims;

namespace Workoutly.Api.Exercises;

public static class ExerciseEndpoints
{
    public static void MapExerciseEndpoints(this WebApplication app)
    {
        var exercises = app.MapGroup("/exercises").RequireAuthorization();
        exercises.MapGet("/", async (ClaimsPrincipal user, ExerciseService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.ListAsync(user.FindFirstValue(ClaimTypes.NameIdentifier)!, null, cancellationToken)));
        exercises.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal user, ExerciseService service, CancellationToken cancellationToken) =>
        {
            var matches = await service.ListAsync(user.FindFirstValue(ClaimTypes.NameIdentifier)!, id.ToString(), cancellationToken);
            return matches.Count == 0 ? Results.NotFound() : Results.Ok(matches[0]);
        });
        exercises.MapPost("/", async (CreateExerciseRequest request, ClaimsPrincipal user, ExerciseService service, CancellationToken cancellationToken) =>
        {
            var errors = new Dictionary<string, string[]>();
            if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length is < 2 or > 120)
                errors["name"] = ["Informe um nome entre 2 e 120 caracteres."];
            if (string.IsNullOrWhiteSpace(request.MuscleGroup) || request.MuscleGroup.Trim().Length > 60)
                errors["muscleGroup"] = ["Informe o grupo muscular com até 60 caracteres."];
            if (string.IsNullOrWhiteSpace(request.Equipment) || request.Equipment.Trim().Length > 80)
                errors["equipment"] = ["Informe o equipamento com até 80 caracteres (ou Peso corporal)."];
            if (request.Description?.Trim().Length > 1000)
                errors["description"] = ["Use até 1000 caracteres na descrição."];
            if (errors.Count > 0)
                return Results.ValidationProblem(errors);
            var exercise = await service.CreateAsync(user.FindFirstValue(ClaimTypes.NameIdentifier)!, request.Name!, request.MuscleGroup!, request.Equipment!, request.Description, cancellationToken);
            return Results.Created($"/exercises/{exercise.Id}", exercise);
        });
    }

    public sealed record CreateExerciseRequest(string? Name, string? MuscleGroup, string? Equipment, string? Description);
}
