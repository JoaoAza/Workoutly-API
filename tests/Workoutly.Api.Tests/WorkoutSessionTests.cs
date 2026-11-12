using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Workoutly.Api.Data;
using Workoutly.Api.Auth;

namespace Workoutly.Api.Tests;

public sealed class WorkoutSessionTests(PostgresFixture mysql) : IClassFixture<PostgresFixture>
{
    [PostgresFact, Trait("Category", "Postgres")]
    public async Task Student_saves_actual_sets_once_and_recovers_the_executed_revision_after_plan_changes_or_deletion()
    {
        await using var app = new DatabaseApplication(mysql.ConnectionString);
        await app.Services.GetRequiredService<PostgresDatabase>().MigrateAsync();
        using var student = await Student(app);
        var exerciseId = (await student.GetFromJsonAsync<ExerciseBody[]>("/exercises"))![0].Id;
        var planId = Guid.NewGuid();
        var planned = new[] { new { exerciseId, sets = 3, repetitions = 12 } };
        Assert.Equal(HttpStatusCode.Created, (await student.PostAsJsonAsync("/plans", new { id = planId, name = "Plano original", notes = "", exercises = planned })).StatusCode);
        Assert.Empty((await student.GetFromJsonAsync<SummaryBody[]>("/sessions"))!);
        var id = Guid.NewGuid();
        var startedAt = DateTimeOffset.UtcNow.AddMinutes(-30);
        var payload = new { id, planId, planVersion = 1, startedAt, durationSeconds = 1500, notes = " Carga confortável ", exercises = new[] { new { exerciseId, sets = new[] { new { repetitions = 10, weightKg = 22.5m }, new { repetitions = 8, weightKg = 25m } } } } };
        // O aluno já abriu a revisão 1; alterações da ficha não mudam o treino em andamento.
        await student.PutAsJsonAsync($"/plans/{planId}", new { version = 1, name = "Plano revisado", notes = "", exercises = new[] { new { exerciseId, sets = 4, repetitions = 15 } } });
        await student.DeleteAsync($"/plans/{planId}?version=2");
        var saved = await student.PostAsJsonAsync("/sessions", payload);
        Assert.Equal(HttpStatusCode.Created, saved.StatusCode);
        Assert.Equal("no-store", saved.Headers.CacheControl?.ToString());
        var session = (await saved.Content.ReadFromJsonAsync<SessionBody>())!;
        Assert.Equal(id.ToString(), session.Id);
        Assert.Equal("Plano original", session.PlanName);
        Assert.Equal(1, session.PlanVersion);
        Assert.Equal(1500, session.DurationSeconds);
        Assert.Equal("Carga confortável", session.Notes);
        var exercise = Assert.Single(session.Exercises);
        Assert.Equal(3, exercise.PlannedSets);
        Assert.Equal(12, exercise.PlannedRepetitions);
        Assert.Equal(new[] { new SetBody(10, 22.5m), new SetBody(8, 25m) }, exercise.Sets);
        Assert.Equal(HttpStatusCode.OK, (await student.PostAsJsonAsync("/sessions", payload)).StatusCode);
        var summary = Assert.Single((await student.GetFromJsonAsync<SummaryBody[]>("/sessions"))!);
        Assert.Equal(2, summary.SetCount);
        Assert.Equal(1, summary.ExerciseCount);
        Assert.Equal("Plano original", summary.PlanName);
        await using var restarted = new DatabaseApplication(mysql.ConnectionString);
        using var restored = restarted.CreateClient();
        restored.DefaultRequestHeaders.Authorization = student.DefaultRequestHeaders.Authorization;
        var recovered = (await restored.GetFromJsonAsync<SessionBody>($"/sessions/{id}"))!;
        Assert.Equal(session.Notes, recovered.Notes);
        Assert.Equal(session.StartedAt, recovered.StartedAt);
        Assert.Equal(exercise.Sets, recovered.Exercises[0].Sets);
        Assert.Equal(HttpStatusCode.NotFound, (await restored.GetAsync($"/plans/{planId}")).StatusCode);
    }

    [PostgresFact, Trait("Category", "Postgres")]
    public async Task Sessions_are_private_and_student_identity_comes_only_from_the_authenticated_account()
    {
        await using var app = new DatabaseApplication(mysql.ConnectionString);
        await app.Services.GetRequiredService<PostgresDatabase>().MigrateAsync();
        using var student = await Student(app);
        using var other = await Student(app);
        var owner = (await student.GetFromJsonAsync<AccountProfile>("/auth/me"))!;
        var exerciseId = (await student.GetFromJsonAsync<ExerciseBody[]>("/exercises"))![0].Id;
        var planId = Guid.NewGuid();
        await student.PostAsJsonAsync("/plans", new { id = planId, name = "Plano privado", notes = "", exercises = new[] { new { exerciseId, sets = 2, repetitions = 10 } } });
        var id = Guid.NewGuid();
        var payload = new { id, planId, planVersion = 1, studentId = Guid.NewGuid(), startedAt = DateTimeOffset.UtcNow.AddMinutes(-5), durationSeconds = 120, notes = "", exercises = new[] { new { exerciseId, name = "Descrição adulterada", sets = new[] { new { repetitions = 7, weightKg = 0m } } } } };
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsJsonAsync("/sessions", payload)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await student.PostAsJsonAsync("/sessions", payload)).StatusCode);
        var saved = (await student.GetFromJsonAsync<SessionBody>($"/sessions/{id}"))!;
        Assert.Equal(owner.Id, saved.StudentId);
        Assert.NotEqual("Descrição adulterada", saved.Exercises[0].Name);
        Assert.Equal(0m, saved.Exercises[0].Sets[0].WeightKg);
        Assert.Empty((await other.GetFromJsonAsync<SummaryBody[]>("/sessions"))!);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/sessions/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/sessions/{Guid.NewGuid()}")).StatusCode);
        var email = $"{Guid.NewGuid():N}@example.test";
        await app.Services.GetRequiredService<AuthService>().CreateTeacherAsync("Professor Treino", email, "Treino2026!seguro", default);
        using var teacher = app.CreateClient();
        var login = await teacher.PostAsJsonAsync("/auth/login", new { email, password = "Treino2026!seguro" });
        teacher.DefaultRequestHeaders.Authorization = new("Bearer", (await login.Content.ReadFromJsonAsync<LoginBody>())!.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await teacher.GetAsync("/sessions")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await teacher.GetAsync($"/sessions/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await teacher.PostAsJsonAsync("/sessions", payload)).StatusCode);
        using var anonymous = app.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/sessions")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/sessions", payload)).StatusCode);
    }

    [PostgresFact, Trait("Category", "Postgres")]
    public async Task Validation_is_atomic_and_concurrent_or_equivalent_retries_do_not_duplicate_or_overwrite_a_session()
    {
        await using var app = new DatabaseApplication(mysql.ConnectionString);
        await app.Services.GetRequiredService<PostgresDatabase>().MigrateAsync();
        using var student = await Student(app);
        var exercises = (await student.GetFromJsonAsync<ExerciseBody[]>("/exercises"))!;
        var first = exercises[0].Id;
        var second = exercises[1].Id;
        var planId = Guid.NewGuid();
        await student.PostAsJsonAsync("/plans", new { id = planId, name = "Treino validado", notes = "", exercises = new[] { new { exerciseId = first, sets = 2, repetitions = 10 }, new { exerciseId = second, sets = 2, repetitions = 12 } } });
        object Set(int repetitions = 10, decimal weightKg = 0) => new { repetitions, weightKg };
        object Exercise(string exerciseId, object[]? sets) => new { exerciseId, sets };
        var startedAt = DateTimeOffset.UtcNow.AddMinutes(-15);
        async Task Invalid(object[]? items, int duration = 600, string notes = "", DateTimeOffset? start = null, int version = 1)
        {
            var result = await student.PostAsJsonAsync("/sessions", new { id = Guid.NewGuid(), planId, planVersion = version, startedAt = start ?? startedAt, durationSeconds = duration, notes, exercises = items });
            Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        }
        await Invalid(null);
        await Invalid([]);
        await Invalid([Exercise(first, null)]);
        await Invalid([Exercise(first, [])]);
        await Invalid([Exercise(first, [Set(0)])]);
        await Invalid([Exercise(first, [Set(201)])]);
        await Invalid([Exercise(first, [Set(weightKg: -1)])]);
        await Invalid([Exercise(first, [Set(weightKg: 2000.01m)])]);
        await Invalid([Exercise(first, [Set(weightKg: 10.125m)])]);
        await Invalid([Exercise(first, [Set()]), Exercise(first, [Set()])]);
        await Invalid([Exercise(first, Enumerable.Repeat(Set(), 21).ToArray())]);
        await Invalid([Exercise(first, [Set()])], duration: 0);
        await Invalid([Exercise(first, [Set()])], duration: 86401);
        await Invalid([Exercise(first, [Set()])], notes: new string('a', 1001));
        await Invalid([Exercise(first, [Set()])], start: DateTimeOffset.UtcNow.AddHours(1));
        await Invalid([Exercise(first, [Set()])], start: DateTimeOffset.UtcNow, duration: 3600);
        await Invalid([Exercise(first, [Set()])], version: 0);
        // Um exercício válido do catálogo também é recusado se não pertence à revisão executada.
        await Invalid([Exercise(first, [Set()]), Exercise(exercises[2].Id, [Set()])]);
        Assert.Empty((await student.GetFromJsonAsync<SummaryBody[]>("/sessions"))!);
        var id = Guid.NewGuid();
        var items = new[] { Exercise(second, [Set(11, 22.5m)]), Exercise(first, [Set(9, 10m)]) };
        var payload = new { id, planId, planVersion = 1, startedAt, durationSeconds = 600, notes = " Treino de teste ", exercises = items };
        var responses = await Task.WhenAll(student.PostAsJsonAsync("/sessions", payload), student.PostAsJsonAsync("/sessions", payload));
        Assert.Contains(responses, r => r.StatusCode == HttpStatusCode.Created);
        Assert.Contains(responses, r => r.StatusCode == HttpStatusCode.OK);
        var equivalent = new { id, planId, planVersion = 1, startedAt = startedAt.ToOffset(TimeSpan.FromHours(-3)), durationSeconds = 600, notes = "Treino de teste", exercises = new[] { Exercise(first, [Set(9, 10.00m)]), Exercise(second, [Set(11, 22.50m)]) } };
        Assert.Equal(HttpStatusCode.OK, (await student.PostAsJsonAsync("/sessions", equivalent)).StatusCode);
        var changed = new { id, planId, planVersion = 1, startedAt, durationSeconds = 600, notes = "Não sobrescrever", exercises = items };
        Assert.Equal(HttpStatusCode.Conflict, (await student.PostAsJsonAsync("/sessions", changed)).StatusCode);
        Assert.Single((await student.GetFromJsonAsync<SummaryBody[]>("/sessions"))!);
        var saved = (await student.GetFromJsonAsync<SessionBody>($"/sessions/{id}"))!;
        Assert.Equal("Treino de teste", saved.Notes);
        Assert.Equal(new[] { first, second }, saved.Exercises.Select(e => e.ExerciseId));
    }

    private static async Task<HttpClient> Student(DatabaseApplication app)
    {
        var client = app.CreateClient();
        var response = await client.PostAsJsonAsync("/auth/register", new { name = "Aluno Treino", email = $"{Guid.NewGuid():N}@example.test", password = "Treino2026!seguro" });
        client.DefaultRequestHeaders.Authorization = new("Bearer", (await response.Content.ReadFromJsonAsync<LoginBody>())!.AccessToken);
        return client;
    }
    private sealed record LoginBody(string AccessToken);
    private sealed record ExerciseBody(string Id, string Name);
    private sealed record SetBody(int Repetitions, decimal WeightKg);
    private sealed record SessionExerciseBody(string ExerciseId, string Name, int PlannedSets, int PlannedRepetitions, SetBody[] Sets);
    private sealed record SessionBody(string Id, string StudentId, string PlanId, int PlanVersion, string PlanName, DateTimeOffset StartedAt, int DurationSeconds, string Notes, SessionExerciseBody[] Exercises);
    private sealed record SummaryBody(string Id, string PlanName, DateTimeOffset StartedAt, int DurationSeconds, int ExerciseCount, int SetCount);
}
