using System.Net;
using System.Net.Http.Json;

namespace Workoutly.Api.Tests;

public sealed class AccountValidationTests
{
    [Fact]
    public async Task Liveness_does_not_consult_database_even_if_client_sends_an_old_token()
    {
        await using var application = new DatabaseApplication(null);
        using var client = application.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", new string('x', 43));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
    }

    [Theory]
    [InlineData("", "aluno@example.test", "Treino2026!seguro")]
    [InlineData("Aluno", "nao-e-email", "Treino2026!seguro")]
    [InlineData("Aluno", "aluno@example.test", "curta")]
    public async Task Invalid_registration_is_rejected_before_accessing_database(string name, string email, string password)
    {
        await using var application = new DatabaseApplication(null);
        using var client = application.CreateClient();
        var response = await client.PostAsJsonAsync("/auth/register", new { name, email, password });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("coach")]
    [InlineData("")]
    public async Task Registration_rejects_an_unknown_role_before_accessing_database(string role)
    {
        await using var application = new DatabaseApplication(null);
        using var client = application.CreateClient();
        var response = await client.PostAsJsonAsync("/auth/register", new
        {
            name = "Perfil Teste", email = "perfil@example.test", password = "Treino2026!seguro", role
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Profile_requires_a_session_and_repeated_attempts_are_limited()
    {
        await using var application = new DatabaseApplication(null);
        using var client = application.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/auth/me")).StatusCode);
        for (var attempt = 0; attempt < 20; attempt++)
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/auth/login", new { email = "invalid", password = "" })).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsJsonAsync("/auth/login", new { email = "invalid", password = "" })).StatusCode);
    }
}
