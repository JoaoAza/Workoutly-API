using System.Security.Claims;

namespace Workoutly.Api.Plans;

public static class PlanEndpoints
{
    public static void MapPlanEndpoints(this WebApplication app)
    {
        Map(app.MapGroup("/plans").RequireAuthorization("student"), false);
        Map(app.MapGroup("/teaching/students/{studentId:guid}/plans").RequireAuthorization("teacher"), true);
    }

    private static void Map(RouteGroupBuilder group, bool teacher)
    {
        string Actor(ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.NameIdentifier)!;
        string Student(HttpContext context, ClaimsPrincipal user) => teacher ? ((string)context.Request.RouteValues["studentId"]!).ToLowerInvariant() : Actor(user);
        group.MapGet("/", async (HttpContext context, ClaimsPrincipal user, PlanService service, CancellationToken ct) =>
        {
            var plans = await service.ListAsync(Actor(user), Student(context, user), teacher, null, null, ct);
            return plans is null ? Results.NotFound() : Results.Ok(plans);
        });
        group.MapGet("/{id:guid}", async (Guid id, int? version, HttpContext context, ClaimsPrincipal user, PlanService service, CancellationToken ct) =>
        {
            var plans = await service.ListAsync(Actor(user), Student(context, user), teacher, id.ToString(), version, ct);
            return plans is not { Count: > 0 } ? Results.NotFound() : Results.Ok(plans[0]);
        });
        group.MapPost("/", async (CreatePlanRequest request, HttpContext context, ClaimsPrincipal user, PlanService service, CancellationToken ct) =>
        {
            var errors = Validate(request.Name, request.Notes, request.Exercises);
            if (request.Id == Guid.Empty) errors["id"] = ["Informe um identificador válido para a ficha."];
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            return SaveResult(await service.SaveAsync(Actor(user), Student(context, user), teacher, request.Id.ToString(), 0, request.Name!, request.Notes, request.Exercises!, ct));
        });
        group.MapPut("/{id:guid}", async (Guid id, UpdatePlanRequest request, HttpContext context, ClaimsPrincipal user, PlanService service, CancellationToken ct) =>
        {
            var errors = Validate(request.Name, request.Notes, request.Exercises);
            if (request.Version < 1 || request.Version == int.MaxValue) errors["version"] = ["Informe a versão atual da ficha."];
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            return SaveResult(await service.SaveAsync(Actor(user), Student(context, user), teacher, id.ToString(), request.Version, request.Name!, request.Notes, request.Exercises!, ct));
        });
        group.MapDelete("/{id:guid}", async (Guid id, int version, HttpContext context, ClaimsPrincipal user, PlanService service, CancellationToken ct) =>
        {
            if (version < 1) return Results.ValidationProblem(new Dictionary<string, string[]> { ["version"] = ["Informe a versão atual da ficha."] });
            return await service.DeleteAsync(Actor(user), Student(context, user), teacher, id.ToString(), version, ct) switch
            {
                "deleted" => Results.NoContent(), "conflict" => Conflict(), _ => Results.NotFound()
            };
        });
    }

    private static IResult SaveResult(PlanResult result) => result.Status switch
    {
        "created" => Results.Json(result.Plan, statusCode: 201),
        "updated" or "existing" => Results.Ok(result.Plan),
        "conflict" => Conflict(),
        "exercise_invalid" => Results.ValidationProblem(new Dictionary<string, string[]> { ["exercises"] = ["Um exercício não está disponível. Selecione itens do seu catálogo."] }),
        _ => Results.NotFound()
    };
    private static IResult Conflict() => Results.Conflict(new { message = "A ficha já foi salva, alterada ou excluída. Atualize a lista e abra a versão atual antes de tentar novamente." });
    private static Dictionary<string, string[]> Validate(string? name, string? notes, PlanExerciseRequest[]? exercises)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length is < 2 or > 120) errors["name"] = ["Informe um nome entre 2 e 120 caracteres."];
        if (notes?.Trim().Length > 1000) errors["notes"] = ["Use até 1000 caracteres nas observações."];
        if (exercises is null || exercises.Length is < 1 or > 50 || exercises.Any(e => e is null || e.ExerciseId == Guid.Empty || e.Sets is < 1 or > 20 || e.Repetitions is < 1 or > 200) || exercises.Select(e => e.ExerciseId).Distinct().Count() != exercises.Length)
            errors["exercises"] = ["Inclua de 1 a 50 exercícios distintos, com 1 a 20 séries e 1 a 200 repetições cada."];
        return errors;
    }
    public sealed record CreatePlanRequest(Guid Id, string? Name, string? Notes, PlanExerciseRequest[]? Exercises);
    public sealed record UpdatePlanRequest(int Version, string? Name, string? Notes, PlanExerciseRequest[]? Exercises);
}
