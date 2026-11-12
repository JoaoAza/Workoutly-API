using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Workoutly.Api.Auth;
using Workoutly.Api.Data;

namespace Workoutly.Api.Tests;

public sealed class WorkoutHistoryTests(PostgresFixture mysql) : IClassFixture<PostgresFixture>
{
    private const string Period = "from=2025-04-01&to=2025-04-03&offsetMinutes=-180";

    [PostgresFact, Trait("Category", "Postgres")]
    public async Task History_uses_actual_sets_and_local_dates_and_recalculates_after_correction_and_plan_deletion()
    {
        await using var app = new DatabaseApplication(mysql.ConnectionString);
        await app.Services.GetRequiredService<PostgresDatabase>().MigrateAsync();
        using var student = await Student(app);
        var (plan, exercise) = await Plan(student);
        await Save(student, plan, exercise, "2025-04-01T02:59:59Z", [new(10, 100)]);
        var first = await Save(student, plan, exercise, "2025-04-01T03:00:00Z", [new(10, 20), new(8, 25)]);
        await Save(student, plan, exercise, "2025-04-02T02:00:00Z", [new(5, 30)]);
        await Save(student, plan, exercise, "2025-04-02T15:00:00Z", [new(12, 0)]);
        await Save(student, plan, exercise, "2025-04-04T03:00:00Z", [new(10, 200)]);
        var response = await student.GetAsync($"/history?{Period}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        var history = (await response.Content.ReadFromJsonAsync<HistoryBody>())!;
        Assert.Equal(3, history.SessionCount); Assert.Equal(2, history.ActiveDays);
        Assert.Equal(4, history.SetCount); Assert.Equal(35, history.RepetitionCount);
        Assert.Equal(1800, history.DurationSeconds); Assert.Equal(550m, history.VolumeKg);
        Assert.Equal(3, history.Sessions.Length);
        Assert.Equal(new[] { new DayBody(new(2025, 4, 1), 2), new DayBody(new(2025, 4, 2), 1) }, history.Days);
        var evolution = Assert.Single(history.Exercises);
        Assert.Equal(exercise, evolution.ExerciseId);
        Assert.Equal(new[] { 25m, 30m, 0m }, evolution.Points.Select(p => p.MaxWeightKg));
        Assert.Equal(new[] { 400m, 150m, 0m }, evolution.Points.Select(p => p.VolumeKg));
        Assert.Equal(new[] { 18, 5, 12 }, evolution.Points.Select(p => p.RepetitionCount));
        Assert.Equal(HttpStatusCode.NoContent, (await student.DeleteAsync($"/plans/{plan}?version=1")).StatusCode);
        var correction = await student.PutAsJsonAsync($"/sessions/{first}", new { editId = Guid.NewGuid(), version = 1, durationSeconds = 900, notes = "Corrigido", exercises = new[] { new { exerciseId = exercise, sets = new[] { new SetBody(2, 40) } } } });
        Assert.Equal(HttpStatusCode.OK, correction.StatusCode);
        var corrected = (await student.GetFromJsonAsync<HistoryBody>($"/history?{Period}"))!;
        Assert.Equal(3, corrected.SessionCount); Assert.Equal(3, corrected.SetCount);
        Assert.Equal(19, corrected.RepetitionCount); Assert.Equal(2100, corrected.DurationSeconds);
        Assert.Equal(230m, corrected.VolumeKg);
        Assert.Equal(40m, Assert.Single(corrected.Exercises).Points[0].MaxWeightKg);
        await using var restarted = new DatabaseApplication(mysql.ConnectionString);
        using var restored = restarted.CreateClient();
        restored.DefaultRequestHeaders.Authorization = student.DefaultRequestHeaders.Authorization;
        Assert.Equal(230m, (await restored.GetFromJsonAsync<HistoryBody>($"/history?{Period}"))!.VolumeKg);
        var empty = (await student.GetFromJsonAsync<HistoryBody>("/history?from=2024-01-01&to=2024-01-01&offsetMinutes=-180"))!;
        Assert.Equal(0, empty.SessionCount); Assert.Equal(0m, empty.VolumeKg);
        Assert.Empty(empty.Sessions); Assert.Empty(empty.Days); Assert.Empty(empty.Exercises);
    }

    [PostgresFact, Trait("Category", "Postgres")]
    public async Task Only_the_currently_linked_teacher_can_read_history_and_details_and_cannot_correct_student_sessions()
    {
        await using var app = new DatabaseApplication(mysql.ConnectionString);
        await app.Services.GetRequiredService<PostgresDatabase>().MigrateAsync();
        using var student = await Student(app);
        using var stranger = await Student(app);
        using var teacher = await Teacher(app);
        using var otherTeacher = await Teacher(app);
        var studentId = (await student.GetFromJsonAsync<UserBody>("/auth/me"))!.Id;
        var (plan, exercise) = await Plan(student);
        var session = await Save(student, plan, exercise, "2025-04-01T03:00:00Z", [new(10, 25)]);
        var history = $"/teaching/students/{studentId}/history?{Period}";
        var detail = $"/teaching/students/{studentId}/sessions/{session}";
        Assert.Equal(HttpStatusCode.NotFound, (await teacher.GetAsync(history)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await teacher.GetAsync(detail)).StatusCode);
        var code = (await (await teacher.PostAsync("/teaching/invitation", null)).Content.ReadFromJsonAsync<InvitationBody>())!.Code;
        Assert.Equal(HttpStatusCode.Created, (await student.PostAsJsonAsync("/enrollment", new { code })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await teacher.GetAsync(history)).StatusCode);
        Assert.Equal(250m, (await teacher.GetFromJsonAsync<HistoryBody>(history))!.VolumeKg);
        Assert.Equal(HttpStatusCode.OK, (await teacher.GetAsync(detail)).StatusCode);
        Assert.Equal("no-store", (await teacher.GetAsync(detail)).Headers.CacheControl?.ToString());
        Assert.Equal(HttpStatusCode.NotFound, (await teacher.GetAsync($"/teaching/students/{studentId}/sessions/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await otherTeacher.GetAsync(history)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await otherTeacher.GetAsync(detail)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/sessions/{session}")).StatusCode);
        Assert.Equal(0, (await stranger.GetFromJsonAsync<HistoryBody>($"/history?{Period}&studentId={studentId}"))!.SessionCount);
        Assert.Equal(HttpStatusCode.Forbidden, (await student.GetAsync(history)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await teacher.GetAsync($"/history?{Period}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await teacher.PutAsJsonAsync($"/sessions/{session}", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await teacher.PutAsJsonAsync(detail, new { })).StatusCode);
        using var anonymous = app.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(history)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/history?{Period}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await student.DeleteAsync("/enrollment")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await teacher.GetAsync(history)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await teacher.GetAsync(detail)).StatusCode);
        Assert.Equal(250m, (await student.GetFromJsonAsync<HistoryBody>($"/history?{Period}"))!.VolumeKg);
        Assert.Equal(HttpStatusCode.OK, (await student.GetAsync($"/sessions/{session}")).StatusCode);
        var secondCode = (await (await otherTeacher.PostAsync("/teaching/invitation", null)).Content.ReadFromJsonAsync<InvitationBody>())!.Code;
        Assert.Equal(HttpStatusCode.Created, (await student.PostAsJsonAsync("/enrollment", new { code = secondCode })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await otherTeacher.GetAsync(history)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await teacher.GetAsync(history)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await otherTeacher.DeleteAsync($"/teaching/students/{studentId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await otherTeacher.GetAsync(detail)).StatusCode);
    }

    [PostgresFact, Trait("Category", "Postgres")]
    public async Task History_rejects_missing_invalid_or_excessive_periods_and_uses_an_explicit_offset()
    {
        await using var app = new DatabaseApplication(mysql.ConnectionString);
        await app.Services.GetRequiredService<PostgresDatabase>().MigrateAsync();
        using var student = await Student(app);
        foreach (var query in new[] { "", "from=oops&to=2025-04-01&offsetMinutes=0", "from=2025-04-02&to=2025-04-01&offsetMinutes=0",
            "from=2024-01-01&to=2025-01-01&offsetMinutes=0", "from=1999-12-31&to=2000-01-01&offsetMinutes=0",
            "from=2025-04-01&to=2025-04-01&offsetMinutes=841", "from=2025-04-01&to=2025-04-01&offsetMinutes=-841",
            "from=9999-12-31&to=9999-12-31&offsetMinutes=0" })
            Assert.Equal(HttpStatusCode.BadRequest, (await student.GetAsync($"/history?{query}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await student.GetAsync("/history?from=2024-01-01&to=2024-12-31&offsetMinutes=840")).StatusCode);
        var (plan, exercise) = await Plan(student);
        await Save(student, plan, exercise, "2025-04-01T02:00:00Z", [new(1, 10)]);
        var utcDay = (await student.GetFromJsonAsync<HistoryBody>("/history?from=2025-04-01&to=2025-04-01&offsetMinutes=0"))!;
        var brazilDay = (await student.GetFromJsonAsync<HistoryBody>("/history?from=2025-04-01&to=2025-04-01&offsetMinutes=-180"))!;
        Assert.Equal(1, utcDay.SessionCount); Assert.Equal(0, brazilDay.SessionCount);
    }

    private static async Task<HttpClient> Teacher(DatabaseApplication app)
    {
        var email = $"{Guid.NewGuid():N}@example.test";
        await app.Services.GetRequiredService<AuthService>().CreateTeacherAsync("Professor Histórico", email, "Treino2026!seguro", default);
        var client = app.CreateClient();
        var response = await client.PostAsJsonAsync("/auth/login", new { email, password = "Treino2026!seguro" });
        client.DefaultRequestHeaders.Authorization = new("Bearer", (await response.Content.ReadFromJsonAsync<LoginBody>())!.AccessToken);
        return client;
    }

    private static async Task<HttpClient> Student(DatabaseApplication app)
    {
        var client = app.CreateClient();
        var response = await client.PostAsJsonAsync("/auth/register", new { name = "Aluno Histórico", email = $"{Guid.NewGuid():N}@example.test", password = "Treino2026!seguro" });
        client.DefaultRequestHeaders.Authorization = new("Bearer", (await response.Content.ReadFromJsonAsync<LoginBody>())!.AccessToken);
        return client;
    }
    private static async Task<(string Plan, string Exercise)> Plan(HttpClient client)
    {
        var exercise = (await client.GetFromJsonAsync<ExerciseBody[]>("/exercises"))![0].Id;
        var plan = Guid.NewGuid().ToString();
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/plans", new { id = plan, name = "Histórico", notes = "", exercises = new[] { new { exerciseId = exercise, sets = 3, repetitions = 12 } } })).StatusCode);
        return (plan, exercise);
    }
    private static async Task<string> Save(HttpClient client, string plan, string exercise, string started, SetBody[] sets)
    {
        var id = Guid.NewGuid().ToString();
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/sessions", new { id, planId = plan, planVersion = 1, startedAt = started, durationSeconds = 600, notes = "", exercises = new[] { new { exerciseId = exercise, sets } } })).StatusCode);
        return id;
    }
    private sealed record LoginBody(string AccessToken);
    private sealed record UserBody(string Id);
    private sealed record InvitationBody(string Code);
    private sealed record ExerciseBody(string Id);
    private sealed record SetBody(int Repetitions, decimal WeightKg);
    private sealed record DayBody(DateOnly Date, int SessionCount);
    private sealed record PointBody(string SessionId, decimal MaxWeightKg, decimal VolumeKg, int RepetitionCount);
    private sealed record EvolutionBody(string ExerciseId, PointBody[] Points);
    private sealed record HistoryBody(int SessionCount, int ActiveDays, int SetCount, long RepetitionCount, long DurationSeconds, decimal VolumeKg, DayBody[] Days, EvolutionBody[] Exercises, object[] Sessions);
}
