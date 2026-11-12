using System.Security.Claims;

namespace Workoutly.Api.Workouts;

public static class WorkoutHistoryEndpoints
{
    public static void MapWorkoutHistoryEndpoints(this WebApplication app)
    {
        app.MapGet("/history", async (DateOnly from, DateOnly to, int offsetMinutes, ClaimsPrincipal user, WorkoutHistoryService service, CancellationToken ct) =>
        {
            if (!ValidPeriod(from, to, offsetMinutes)) return InvalidPeriod();
            return Results.Ok(await service.GetAsync(user.FindFirstValue(ClaimTypes.NameIdentifier)!, from, to, offsetMinutes, ct));
        }).RequireAuthorization("student");
        var teaching = app.MapGroup("/teaching/students/{studentId:guid}").RequireAuthorization("teacher");
        teaching.MapGet("/history", async (Guid studentId, DateOnly from, DateOnly to, int offsetMinutes, ClaimsPrincipal user, WorkoutHistoryService service, CancellationToken ct) =>
        {
            if (!ValidPeriod(from, to, offsetMinutes)) return InvalidPeriod();
            var history = await service.GetAsync(studentId.ToString(), from, to, offsetMinutes, ct, user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            return history is null ? Results.NotFound() : Results.Ok(history);
        });
        teaching.MapGet("/sessions/{id:guid}", async (Guid studentId, Guid id, ClaimsPrincipal user, WorkoutHistoryService service, CancellationToken ct) =>
        {
            var session = await service.GetStudentSessionAsync(user.FindFirstValue(ClaimTypes.NameIdentifier)!, studentId.ToString(), id.ToString(), ct);
            return session is null ? Results.NotFound() : Results.Ok(session);
        });
    }

    private static bool ValidPeriod(DateOnly from, DateOnly to, int offsetMinutes) =>
        from >= new DateOnly(2000, 1, 1) && to < new DateOnly(9999, 12, 31) && to >= from && to.DayNumber - from.DayNumber < 366 && offsetMinutes is >= -840 and <= 840;

    private static IResult InvalidPeriod() => Results.ValidationProblem(new Dictionary<string, string[]>
    {
        ["period"] = ["Informe datas desde 01/01/2000, em ordem, com até 366 dias e fuso entre -840 e 840 minutos."]
    });
}
