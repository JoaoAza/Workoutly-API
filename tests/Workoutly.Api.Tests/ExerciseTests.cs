using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Workoutly.Api.Data;

namespace Workoutly.Api.Tests;

public sealed class ExerciseTests(PostgresFixture mysql) : IClassFixture<PostgresFixture>
{
    [PostgresFact]
    [Trait("Category", "Postgres")]
    public async Task Catalog_requires_login_and_offers_shared_exercises_after_reapplying_migrations()
    {
        await using var app = new DatabaseApplication(mysql.ConnectionString);
        using var client = app.CreateClient();
        var database = app.Services.GetRequiredService<PostgresDatabase>();
        await database.MigrateAsync();
        await database.MigrateAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/exercises")).StatusCode);
        await SignIn(client);
        var catalog = await client.GetFromJsonAsync<ExerciseBody[]>("/exercises");
        Assert.NotNull(catalog);
        Assert.Equal(30, catalog.Length);
        Assert.All(catalog, exercise => Assert.True(exercise.IsShared));
        Assert.All(catalog, exercise =>
        {
            Assert.Contains("Músculos trabalhados:", exercise.Description);
            Assert.InRange(exercise.Description.Length, 30, 1000);
        });
        Assert.Equal(catalog.Length, catalog.Select(exercise => exercise.Id).Distinct().Count());
        var squat = Assert.Single(catalog, exercise => exercise.Name == "Agachamento livre");
        Assert.Equal("Pernas", squat.MuscleGroup);
        Assert.Contains("quadríceps", squat.Description);
        Assert.Contains("glúteo máximo", squat.Description);
        Assert.Contains("sóleo", Assert.Single(catalog, exercise => exercise.Name == "Panturrilha sentado").Description);
        var hammer = Assert.Single(catalog, exercise => exercise.Name == "Rosca martelo");
        Assert.Equal("Braços e antebraços", hammer.MuscleGroup);
        Assert.Contains("braquiorradial", hammer.Description);
        var detail = await client.GetFromJsonAsync<ExerciseBody>($"/exercises/{squat.Id}");
        Assert.Equal(squat, detail);
    }

    [PostgresFact]
    [Trait("Category", "Postgres")]
    public async Task Custom_exercise_persists_and_cannot_be_read_by_another_account()
    {
        await using var app = new DatabaseApplication(mysql.ConnectionString);
        using var owner = app.CreateClient();
        await app.Services.GetRequiredService<PostgresDatabase>().MigrateAsync();
        var token = await SignIn(owner);
        using var other = app.CreateClient();
        await SignIn(other);
        var payload = new { name = "  Remada de teste  ", muscleGroup = " Costas ", equipment = " Halteres ", description = " Minha variação. ", ownerId = "outro", isShared = true };
        var created = await owner.PostAsJsonAsync("/exercises", payload);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var exercise = await created.Content.ReadFromJsonAsync<ExerciseBody>();
        Assert.NotNull(exercise);
        Assert.Equal("Remada de teste", exercise.Name);
        Assert.Equal("Costas", exercise.MuscleGroup);
        Assert.Equal("Halteres", exercise.Equipment);
        Assert.Equal("Minha variação.", exercise.Description);
        Assert.False(exercise.IsShared);
        Assert.Equal($"/exercises/{exercise.Id}", created.Headers.Location?.OriginalString);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/exercises/{exercise.Id}")).StatusCode);
        Assert.DoesNotContain((await other.GetFromJsonAsync<ExerciseBody[]>("/exercises"))!, item => item.Id == exercise.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/exercises/{Guid.NewGuid()}")).StatusCode);

        await using var restarted = new DatabaseApplication(mysql.ConnectionString);
        await restarted.Services.GetRequiredService<PostgresDatabase>().MigrateAsync();
        using var restored = restarted.CreateClient();
        restored.DefaultRequestHeaders.Authorization = new("Bearer", token);
        Assert.Equal(exercise, await restored.GetFromJsonAsync<ExerciseBody>($"/exercises/{exercise.Id}"));
        Assert.Contains((await restored.GetFromJsonAsync<ExerciseBody[]>("/exercises"))!, item => item == exercise);
        using var anonymous = app.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/exercises", payload)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/exercises/{exercise.Id}")).StatusCode);
    }

    [PostgresFact]
    [Trait("Category", "Postgres")]
    public async Task Invalid_exercises_are_rejected_without_adding_records_and_limits_are_accepted()
    {
        await using var app = new DatabaseApplication(mysql.ConnectionString);
        using var client = app.CreateClient();
        await app.Services.GetRequiredService<PostgresDatabase>().MigrateAsync();
        await SignIn(client);
        var invalid = new object[]
        {
            new { name = "A", muscleGroup = "Costas", equipment = "Barra", description = "" },
            new { name = new string('x', 121), muscleGroup = "Costas", equipment = "Barra", description = "" },
            new { name = "Remada", muscleGroup = " ", equipment = "Barra", description = "" },
            new { name = "Remada", muscleGroup = new string('x', 61), equipment = "Barra", description = "" },
            new { name = "Remada", muscleGroup = "Costas", equipment = " ", description = "" },
            new { name = "Remada", muscleGroup = "Costas", equipment = new string('x', 81), description = "" },
            new { name = "Remada", muscleGroup = "Costas", equipment = "Barra", description = new string('x', 1001) },
            new { },
        };
        foreach (var payload in invalid)
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/exercises", payload)).StatusCode);
        Assert.Equal(30, (await client.GetFromJsonAsync<ExerciseBody[]>("/exercises"))?.Length);
        var boundary = await client.PostAsJsonAsync("/exercises", new
        {
            name = new string('n', 120), muscleGroup = new string('g', 60), equipment = new string('e', 80), description = new string('d', 1000)
        });
        Assert.Equal(HttpStatusCode.Created, boundary.StatusCode);
    }

    private static async Task<string> SignIn(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/auth/register", new
        {
            name = "Aluno Catálogo", email = $"{Guid.NewGuid():N}@example.test", password = "Treino2026!seguro"
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var session = await response.Content.ReadFromJsonAsync<SessionBody>();
        Assert.NotNull(session);
        client.DefaultRequestHeaders.Authorization = new("Bearer", session.AccessToken);
        return session.AccessToken;
    }

    private sealed record SessionBody(string AccessToken);
    private sealed record ExerciseBody(string Id, string Name, string MuscleGroup, string Equipment, string Description, bool IsShared);
}
