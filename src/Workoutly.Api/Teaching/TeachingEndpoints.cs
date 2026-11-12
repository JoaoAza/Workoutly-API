using System.Security.Claims;

namespace Workoutly.Api.Teaching;

public static class TeachingEndpoints
{
    public static void MapTeachingEndpoints(this WebApplication app)
    {
        var teaching = app.MapGroup("/teaching").RequireAuthorization("teacher");
        teaching.MapGet("/students", async (ClaimsPrincipal user, TeachingService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.ListStudentsAsync(user.FindFirstValue(ClaimTypes.NameIdentifier)!, cancellationToken)));
        teaching.MapGet("/students/{id:guid}", async (Guid id, ClaimsPrincipal user, TeachingService service, CancellationToken cancellationToken) =>
        {
            var students = await service.ListStudentsAsync(user.FindFirstValue(ClaimTypes.NameIdentifier)!, cancellationToken, id.ToString());
            return students.Count == 0 ? Results.NotFound() : Results.Ok(students[0]);
        });
        teaching.MapDelete("/students/{id:guid}", async (Guid id, ClaimsPrincipal user, TeachingService service, CancellationToken cancellationToken) =>
            await service.RemoveStudentAsync(user.FindFirstValue(ClaimTypes.NameIdentifier)!, id.ToString(), cancellationToken) ? Results.NoContent() : Results.NotFound());
        teaching.MapGet("/invitation", async (ClaimsPrincipal user, TeachingService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetInvitationAsync(user.FindFirstValue(ClaimTypes.NameIdentifier)!, cancellationToken)));
        teaching.MapPost("/invitation", async (ClaimsPrincipal user, TeachingService service, CancellationToken cancellationToken) =>
            Results.Json(await service.CreateInvitationAsync(user.FindFirstValue(ClaimTypes.NameIdentifier)!, cancellationToken), statusCode: StatusCodes.Status201Created))
            .RequireRateLimiting("account");

        var enrollment = app.MapGroup("/enrollment").RequireAuthorization("student");
        enrollment.MapGet("/", async (ClaimsPrincipal user, TeachingService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetEnrollmentAsync(user.FindFirstValue(ClaimTypes.NameIdentifier)!, cancellationToken)));
        enrollment.MapPost("/", async (EnrollmentRequest request, ClaimsPrincipal user, TeachingService service, CancellationToken cancellationToken) =>
        {
            var code = request.Code?.Trim().ToUpperInvariant();
            if (code is null || code.Length != 12 || code.Any(character => !char.IsAsciiHexDigit(character)))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["code"] = ["Informe o código de 12 caracteres enviado pelo professor."] });
            var result = await service.JoinAsync(user.FindFirstValue(ClaimTypes.NameIdentifier)!, code, cancellationToken);
            return result.Result switch
            {
                "created" => Results.Json(result.Enrollment, statusCode: StatusCodes.Status201Created),
                "existing" => Results.Ok(result.Enrollment),
                "conflict" => Results.Conflict(new { message = "Você já tem um professor vinculado. Remova o vínculo atual antes de trocar." }),
                _ => Results.ValidationProblem(new Dictionary<string, string[]> { ["code"] = ["Código inválido ou expirado. Confira com seu professor."] })
            };
        }).RequireRateLimiting("account");
        enrollment.MapDelete("/", async (ClaimsPrincipal user, TeachingService service, CancellationToken cancellationToken) =>
        {
            await service.LeaveAsync(user.FindFirstValue(ClaimTypes.NameIdentifier)!, cancellationToken);
            return Results.NoContent();
        });
    }

    public sealed record EnrollmentRequest(string? Code);
}
