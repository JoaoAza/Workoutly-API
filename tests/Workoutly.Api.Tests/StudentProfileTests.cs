using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Workoutly.Api.Data;

namespace Workoutly.Api.Tests;

public sealed class StudentProfileTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string Path = "/auth/me/student-profile";

    [PostgresFact, Trait("Category", "Postgres")]
    public async Task Student_can_complete_and_clear_own_profile_and_teacher_access_is_denied()
    {
        await using var app = new DatabaseApplication(postgres.ConnectionString);
        await app.Services.GetRequiredService<PostgresDatabase>().MigrateAsync();
        using var student = await Register(app, "student", "Nome Anterior");
        using var other = await Register(app, "student", "Outro Aluno");
        using var teacher = await Register(app, "teacher", "Professor Teste");

        var initial = (await student.GetFromJsonAsync<Profile>(Path))!;
        Assert.Equal("Nome Anterior", initial.Name);
        Assert.Null(initial.BodyWeightKg);
        Assert.Null(initial.HeightCm);
        var invalid = await student.PutAsJsonAsync(Path, new { firstName = "Nome", lastName = "Aluno", bodyWeightKg = 70.123m, heightCm = 301 });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        var data = new { firstName = "  Nome  ", lastName = "Aluno", bodyWeightKg = 72.5m, heightCm = 178 };
        Assert.Equal(HttpStatusCode.Forbidden, (await teacher.GetAsync(Path)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await teacher.PutAsJsonAsync(Path, data)).StatusCode);
        using var response = await student.PutAsJsonAsync(Path, data);
        response.EnsureSuccessStatusCode();
        var saved = (await student.GetFromJsonAsync<Profile>(Path))!;
        Assert.Equal(new Profile("Nome Aluno", "Nome", "Aluno", 72.5m, 178), saved);
        Assert.Equal("Nome Aluno", (await student.GetFromJsonAsync<Profile>("/auth/me"))!.Name);

        var unrelated = (await other.GetFromJsonAsync<Profile>(Path))!;
        Assert.Equal("Outro Aluno", unrelated.Name);
        Assert.Null(unrelated.BodyWeightKg);

        (await student.PutAsJsonAsync(Path, new { firstName = "Nome", lastName = "Aluno", bodyWeightKg = (decimal?)null, heightCm = (int?)null })).EnsureSuccessStatusCode();
        var cleared = (await student.GetFromJsonAsync<Profile>(Path))!;
        Assert.Null(cleared.BodyWeightKg);
        Assert.Null(cleared.HeightCm);
    }

    private static async Task<HttpClient> Register(DatabaseApplication app, string role, string name)
    {
        var client = app.CreateClient();
        var response = await client.PostAsJsonAsync("/auth/register", new
        {
            name, role, email = $"{Guid.NewGuid():N}@example.test", password = "Treino2026!seguro"
        });
        response.EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Authorization = new("Bearer", (await response.Content.ReadFromJsonAsync<Session>())!.AccessToken);
        return client;
    }

    private sealed record Session(string AccessToken);
    private sealed record Profile(string Name, string? FirstName, string? LastName, decimal? BodyWeightKg, int? HeightCm);
}
