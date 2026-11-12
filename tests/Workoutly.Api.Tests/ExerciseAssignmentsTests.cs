using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Workoutly.Api.Auth;
using Workoutly.Api.Data;

namespace Workoutly.Api.Tests;

public sealed class ExerciseAssignmentsTests(PostgresFixture mysql) : IClassFixture<PostgresFixture>
{
    [PostgresFact, Trait("Category", "Postgres")]
    public async Task My_exercises_include_owned_and_current_plan_items_without_exposing_other_private_exercises()
    {
        await using var app = new DatabaseApplication(mysql.ConnectionString);
        await app.Services.GetRequiredService<PostgresDatabase>().MigrateAsync();
        async Task<HttpClient> Register(string role)
        {
            var client = app.CreateClient();
            var registered = await client.PostAsJsonAsync("/auth/register", new { name = "Pessoa Fictícia", email = $"{Guid.NewGuid():N}@example.test", password = "Treino2026!seguro", role });
            registered.EnsureSuccessStatusCode();
            client.DefaultRequestHeaders.Authorization = new("Bearer", (await registered.Content.ReadFromJsonAsync<Session>())!.AccessToken);
            return client;
        }
        using var student = await Register("student");
        using var stranger = await Register("student");
        using var teacher = await Register("teacher");
        var studentId = (await student.GetFromJsonAsync<AccountProfile>("/auth/me"))!.Id;
        var invitation = await teacher.PostAsync("/teaching/invitation", null);
        var code = (await invitation.Content.ReadFromJsonAsync<Invitation>())!.Code;
        (await student.PostAsJsonAsync("/enrollment", new { code })).EnsureSuccessStatusCode();
        async Task<Exercise> Add(HttpClient client, string name) =>
            (await (await client.PostAsJsonAsync("/exercises", new { name, muscleGroup = "Costas", equipment = "Halteres", description = "Exemplo privado" })).Content.ReadFromJsonAsync<Exercise>())!;
        var owned = await Add(student, "Movimento próprio");
        var assigned = await Add(teacher, "Movimento recebido na ficha");
        var hidden = await Add(teacher, "Movimento não prescrito");
        var shared = (await student.GetFromJsonAsync<Exercise[]>("/exercises"))!.First(e => e.IsShared);
        var planId = Guid.NewGuid();
        var path = $"/teaching/students/{studentId}/plans";
        (await teacher.PostAsJsonAsync(path, new { id = planId, name = "Ficha do aluno", notes = "", exercises = new[] { new { exerciseId = shared.Id, sets = 3, repetitions = 12 }, new { exerciseId = assigned.Id, sets = 3, repetitions = 10 } } })).EnsureSuccessStatusCode();
        var catalog = (await student.GetFromJsonAsync<Exercise[]>("/exercises"))!;
        Assert.Equal(new[] { owned.Id, assigned.Id, shared.Id }.Order(), catalog.Where(e => e.IsOwned || e.IsInPlan).Select(e => e.Id).Order());
        Assert.DoesNotContain(catalog, e => e.Id == hidden.Id);
        Assert.True((await student.GetFromJsonAsync<Exercise>($"/exercises/{assigned.Id}"))!.IsInPlan);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/exercises/{assigned.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await student.GetAsync($"/exercises/{hidden.Id}")).StatusCode);
        // Itens de fichas repetidas aparecem uma única vez.
        var secondId = Guid.NewGuid();
        (await teacher.PostAsJsonAsync(path, new { id = secondId, name = "Outra ficha", notes = "", exercises = new[] { new { exerciseId = assigned.Id, sets = 3, repetitions = 10 } } })).EnsureSuccessStatusCode();
        Assert.Single((await student.GetFromJsonAsync<Exercise[]>("/exercises"))!, e => e.Id == assigned.Id);
        (await teacher.DeleteAsync($"{path}/{secondId}?version=1")).EnsureSuccessStatusCode();
        // Retirar da revisão atual remove do catálogo recebido, preservando as revisões antigas.
        (await teacher.PutAsJsonAsync($"{path}/{planId}", new { version = 1, name = "Ficha revisada", notes = "", exercises = new[] { new { exerciseId = shared.Id, sets = 3, repetitions = 12 } } })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await student.GetAsync($"/exercises/{assigned.Id}")).StatusCode);
        Assert.False((await student.GetFromJsonAsync<Exercise>($"/exercises/{shared.Id}"))!.IsOwned);
        (await student.DeleteAsync($"/plans/{planId}?version=2")).EnsureSuccessStatusCode();
        Assert.Equal(owned.Id, Assert.Single((await student.GetFromJsonAsync<Exercise[]>("/exercises"))!, e => e.IsOwned || e.IsInPlan).Id);
    }

    private sealed record Session(string AccessToken);
    private sealed record Invitation(string Code);
    private sealed record Exercise(string Id, string Name, bool IsShared, bool IsOwned = false, bool IsInPlan = false);
}
