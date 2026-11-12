using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Workoutly.Api.Auth;
using Workoutly.Api.Data;

namespace Workoutly.Api.Tests;

public sealed class WorkoutCorrectionTests(PostgresFixture mysql) : IClassFixture<PostgresFixture>
{
    [PostgresFact, Trait("Category", "Postgres")]
    public async Task Student_corrects_saved_sets_without_changing_the_source_and_totals_are_recalculated_after_plan_deletion()
    {
        await using var app = new DatabaseApplication(mysql.ConnectionString);
        await app.Services.GetRequiredService<PostgresDatabase>().MigrateAsync();
        using var student = await Student(app);
        var (original, creation) = await CreateWorkout(student);
        var path = $"/sessions/{original.Id}";
        Assert.NotNull(original.PlanExercises);
        var first = original.Exercises[0].ExerciseId;
        var second = original.PlanExercises[1].ExerciseId;
        await student.DeleteAsync($"/plans/{original.PlanId}?version=1");
        var correction = new { editId = Guid.NewGuid(), version = 1, durationSeconds = 1200, notes = " Corrigido ", exercises = new[] {
            new { exerciseId = first, sets = new[] { new SetBody(5, 30), new SetBody(3, 40) } },
            new { exerciseId = second, sets = new[] { new SetBody(12, 10) } } } };
        var edited = await student.PutAsJsonAsync(path, correction);
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        var saved = (await edited.Content.ReadFromJsonAsync<SessionBody>())!;
        Assert.Equal(2, saved.Version);
        Assert.Equal(390m, saved.VolumeKg); // 5*30 + 3*40 + 12*10
        Assert.Equal(400m, original.VolumeKg); // 10*20 + 8*25
        Assert.Equal(1200, saved.DurationSeconds);
        Assert.Equal("Corrigido", saved.Notes);
        Assert.Equal(original.PlanId, saved.PlanId);
        Assert.Equal(original.PlanVersion, saved.PlanVersion);
        Assert.Equal(original.StartedAt, saved.StartedAt);
        Assert.Equal(new[] { new SetBody(5, 30), new SetBody(3, 40) }, saved.Exercises[0].Sets);
        Assert.Equal(2, saved.Exercises.Length);
        var summary = Assert.Single((await student.GetFromJsonAsync<SummaryBody[]>("/sessions"))!);
        Assert.Equal(3, summary.SetCount);
        Assert.Equal(390m, summary.VolumeKg);
        Assert.Equal(2, summary.ExerciseCount);
        Assert.Equal(HttpStatusCode.OK, (await student.PutAsJsonAsync(path, correction)).StatusCode);
        Assert.Equal(2, (await student.GetFromJsonAsync<SessionBody>(path))!.Version);
        // A repetição da criação inicial não pode desfazer a correção.
        var replay = await student.PostAsJsonAsync("/sessions", creation);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal(390m, (await replay.Content.ReadFromJsonAsync<SessionBody>())!.VolumeKg);
        await app.Services.GetRequiredService<PostgresDatabase>().MigrateAsync();
        await using var restarted = new DatabaseApplication(mysql.ConnectionString);
        using var restored = restarted.CreateClient();
        restored.DefaultRequestHeaders.Authorization = student.DefaultRequestHeaders.Authorization;
        var recovered = (await restored.GetFromJsonAsync<SessionBody>(path))!;
        Assert.Equal(2, recovered.Version);
        Assert.Equal("Corrigido", recovered.Notes);
        Assert.Equal(390m, recovered.VolumeKg);
        Assert.Single((await restored.GetFromJsonAsync<SummaryBody[]>("/sessions"))!);
    }
    [PostgresFact, Trait("Category", "Postgres")]
    public async Task Invalid_or_unauthorized_corrections_leave_every_record_unchanged()
    {
        await using var app = new DatabaseApplication(mysql.ConnectionString);
        await app.Services.GetRequiredService<PostgresDatabase>().MigrateAsync();
        using var owner = await Student(app);
        using var other = await Student(app);
        var (session, _) = await CreateWorkout(owner);
        var (otherSession, _) = await CreateWorkout(other);
        var path = $"/sessions/{session.Id}";
        var exerciseId = session.Exercises[0].ExerciseId;
        object Item(string id, SetBody[]? sets) => new { exerciseId = id, sets };
        object Payload(object[]? items, int duration = 1200, string notes = "Correção", int version = 1) => new { editId = Guid.NewGuid(), version, durationSeconds = duration, notes, exercises = items };
        var good = Payload([Item(exerciseId, [new(8, 30)])]);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsJsonAsync(path, good)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.PutAsJsonAsync($"/sessions/{Guid.NewGuid()}", good)).StatusCode);
        var email = $"{Guid.NewGuid():N}@example.test";
        await app.Services.GetRequiredService<AuthService>().CreateTeacherAsync("Professor Correção", email, "Treino2026!seguro", default);
        using var teacher = app.CreateClient();
        var login = await teacher.PostAsJsonAsync("/auth/login", new { email, password = "Treino2026!seguro" });
        teacher.DefaultRequestHeaders.Authorization = new("Bearer", (await login.Content.ReadFromJsonAsync<LoginBody>())!.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await teacher.PutAsJsonAsync(path, good)).StatusCode);
        using var anonymous = app.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PutAsJsonAsync(path, good)).StatusCode);
        async Task Bad(object payload) => Assert.Equal(HttpStatusCode.BadRequest, (await owner.PutAsJsonAsync(path, payload)).StatusCode);
        await Bad(Payload(null)); await Bad(Payload([]));
        await Bad(Payload([Item(exerciseId, null)])); await Bad(Payload([Item(exerciseId, [])]));
        await Bad(Payload([Item(exerciseId, [new(0, 20)])]));
        await Bad(Payload([Item(exerciseId, [new(201, 20)])]));
        await Bad(Payload([Item(exerciseId, [new(10, -1)])]));
        await Bad(Payload([Item(exerciseId, [new(10, 2000.01m)])]));
        await Bad(Payload([Item(exerciseId, [new(10, 20.125m)])]));
        await Bad(Payload([Item(exerciseId, Enumerable.Repeat(new SetBody(10, 20), 21).ToArray())]));
        await Bad(Payload([Item(exerciseId, [new(10, 20)]), Item(exerciseId, [new(8, 25)])]));
        await Bad(Payload([Item(exerciseId, [new(10, 20)])], duration: 0));
        await Bad(Payload([Item(exerciseId, [new(10, 20)])], duration: 86401));
        await Bad(Payload([Item(exerciseId, [new(10, 20)])], duration: 7200)); // início há 1h: terminaria no futuro
        await Bad(Payload([Item(exerciseId, [new(10, 20)])], version: 0));
        await Bad(Payload([Item(exerciseId, [new(10, 20)])], notes: new string('a', 1001)));
        var unavailable = (await owner.GetFromJsonAsync<ExerciseBody[]>("/exercises"))![2].Id;
        await Bad(Payload([Item(exerciseId, [new(1, 1)]), Item(unavailable, [new(1, 1)])]));
        var unchanged = (await owner.GetFromJsonAsync<SessionBody>(path))!;
        Assert.Equal(1, unchanged.Version); Assert.Equal(400m, unchanged.VolumeKg);
        Assert.Equal("Original", unchanged.Notes); Assert.Equal(900, unchanged.DurationSeconds);
        var otherUnchanged = (await other.GetFromJsonAsync<SessionBody>($"/sessions/{otherSession.Id}"))!;
        Assert.Equal(1, otherUnchanged.Version); Assert.Equal(400m, otherUnchanged.VolumeKg);
        // IDs e data enviados como campos extras não podem trocar a origem ou o proprietário.
        var corrected = await owner.PutAsJsonAsync(path, new { editId = Guid.NewGuid(), version = 1, durationSeconds = 1200, notes = "Ajuste próprio", studentId = otherSession.StudentId, planId = otherSession.PlanId, planVersion = 99, startedAt = DateTimeOffset.UtcNow.AddDays(-10), exercises = new[] { Item(exerciseId, [new(8, 30)]) } });
        Assert.Equal(HttpStatusCode.OK, corrected.StatusCode);
        var saved = (await corrected.Content.ReadFromJsonAsync<SessionBody>())!;
        Assert.Equal(session.StudentId, saved.StudentId); Assert.Equal(session.PlanId, saved.PlanId);
        Assert.Equal(session.PlanVersion, saved.PlanVersion); Assert.Equal(session.StartedAt, saved.StartedAt);
        Assert.Equal(240m, saved.VolumeKg);
    }

    [PostgresFact, Trait("Category", "Postgres")]
    public async Task Concurrent_corrections_conflict_and_retries_never_undo_a_newer_correction()
    {
        await using var app = new DatabaseApplication(mysql.ConnectionString);
        await app.Services.GetRequiredService<PostgresDatabase>().MigrateAsync();
        using var student = await Student(app);
        var (session, creation) = await CreateWorkout(student);
        var path = $"/sessions/{session.Id}";
        object Correction(Guid editId, int version, string notes, decimal weight) => new { editId, version, durationSeconds = 1200, notes, exercises = new[] { new { exerciseId = session.Exercises[0].ExerciseId, sets = new[] { new SetBody(10, weight) } } } };
        var firstId = Guid.NewGuid(); var secondId = Guid.NewGuid();
        var first = Correction(firstId, 1, "Edição A", 30);
        var second = Correction(secondId, 1, "Edição B", 40);
        var results = await Task.WhenAll(student.PutAsJsonAsync(path, first), student.PutAsJsonAsync(path, second));
        Assert.Contains(results, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Contains(results, r => r.StatusCode == HttpStatusCode.Conflict);
        var firstWon = results[0].StatusCode == HttpStatusCode.OK;
        var winner = firstWon ? first : second;
        var winnerId = firstWon ? firstId : secondId;
        var current = (await student.GetFromJsonAsync<SessionBody>(path))!;
        Assert.Equal(2, current.Version);
        Assert.Equal(firstWon ? 300m : 400m, current.VolumeKg);
        Assert.Equal(HttpStatusCode.OK, (await student.PutAsJsonAsync(path, winner)).StatusCode);
        Assert.Equal(2, (await student.GetFromJsonAsync<SessionBody>(path))!.Version);
        Assert.Equal(HttpStatusCode.Conflict, (await student.PutAsJsonAsync(path, Correction(winnerId, 1, "Conteúdo diferente", 20))).StatusCode);
        var finalId = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.OK, (await student.PutAsJsonAsync(path, Correction(finalId, 2, "Correção final", 0))).StatusCode);
        var repeatedOld = await student.PutAsJsonAsync(path, winner);
        Assert.Equal(HttpStatusCode.OK, repeatedOld.StatusCode);
        var latest = (await repeatedOld.Content.ReadFromJsonAsync<SessionBody>())!;
        Assert.Equal(3, latest.Version); Assert.Equal("Correção final", latest.Notes); Assert.Equal(0m, latest.VolumeKg);
        var repeatedCreation = await student.PostAsJsonAsync("/sessions", creation);
        Assert.Equal(HttpStatusCode.OK, repeatedCreation.StatusCode);
        Assert.Equal("Correção final", (await repeatedCreation.Content.ReadFromJsonAsync<SessionBody>())!.Notes);
        Assert.Single((await student.GetFromJsonAsync<SummaryBody[]>("/sessions"))!);
    }

    private static async Task<HttpClient> Student(DatabaseApplication app)
    {
        var client = app.CreateClient();
        var response = await client.PostAsJsonAsync("/auth/register", new { name = "Aluno Correção", email = $"{Guid.NewGuid():N}@example.test", password = "Treino2026!seguro" });
        client.DefaultRequestHeaders.Authorization = new("Bearer", (await response.Content.ReadFromJsonAsync<LoginBody>())!.AccessToken);
        return client;
    }
    private static async Task<(SessionBody Session, object Creation)> CreateWorkout(HttpClient client)
    {
        var exercises = (await client.GetFromJsonAsync<ExerciseBody[]>("/exercises"))!;
        var planId = Guid.NewGuid();
        await client.PostAsJsonAsync("/plans", new { id = planId, name = "Treino de correção", notes = "", exercises = exercises.Take(2).Select(e => new { exerciseId = e.Id, sets = 3, repetitions = 12 }).ToArray() });
        var creation = new { id = Guid.NewGuid(), planId, planVersion = 1, startedAt = DateTimeOffset.UtcNow.AddHours(-1), durationSeconds = 900, notes = "Original", exercises = new[] { new { exerciseId = exercises[0].Id, sets = new[] { new SetBody(10, 20), new SetBody(8, 25) } } } };
        var response = await client.PostAsJsonAsync("/sessions", creation);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return ((await response.Content.ReadFromJsonAsync<SessionBody>())!, creation);
    }
    private sealed record LoginBody(string AccessToken);
    private sealed record ExerciseBody(string Id, string Name);
    private sealed record PlanExerciseBody(string ExerciseId);
    private sealed record SetBody(int Repetitions, decimal WeightKg);
    private sealed record ExerciseSessionBody(string ExerciseId, SetBody[] Sets);
    private sealed record SessionBody(string Id, string StudentId, string PlanId, int PlanVersion, DateTimeOffset StartedAt, int DurationSeconds, string Notes, ExerciseSessionBody[] Exercises, int Version, decimal VolumeKg, PlanExerciseBody[] PlanExercises);
    private sealed record SummaryBody(int ExerciseCount, int SetCount, decimal VolumeKg);
}
