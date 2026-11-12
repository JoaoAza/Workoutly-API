using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.TestHost;
using Workoutly.Api.Data;

namespace Workoutly.Api.Tests;

public sealed class AuthenticationTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _mysql;
    public AuthenticationTests(PostgresFixture mysql) => _mysql = mysql;

    [PostgresFact]
    [Trait("Category", "Postgres")]
    public async Task Account_can_register_login_restore_own_profile_and_revoke_session()
    {
        await using var application = new DatabaseApplication(_mysql.ConnectionString);
        using var client = application.CreateClient();
        await application.Services.GetRequiredService<PostgresDatabase>().MigrateAsync();
        var email = $"aluno-{Guid.NewGuid():N}@example.test";
        var registered = await client.PostAsJsonAsync("/auth/register", new { name = "Aluno Teste", email, password = "Treino2026!seguro" });
        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
        var session = await registered.Content.ReadFromJsonAsync<SessionBody>();
        Assert.NotNull(session);
        Assert.Equal(email, session.User.Email);
        Assert.DoesNotContain("password", await registered.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        var profileResponse = await client.GetAsync("/auth/me");
        Assert.True(profileResponse.StatusCode == HttpStatusCode.OK,
            (await profileResponse.Content.ReadAsStringAsync()).Split('\n')[0]);
        Assert.Equal(session.User.Id, (await profileResponse.Content.ReadFromJsonAsync<UserBody>())?.Id);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/auth/me")).StatusCode);

        client.DefaultRequestHeaders.Authorization = null;
        var wrong = await client.PostAsJsonAsync("/auth/login", new { email, password = "Senha-errada-2026" });
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        var login = await client.PostAsJsonAsync("/auth/login", new { email = email.ToUpperInvariant(), password = "Treino2026!seguro" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var newSession = await login.Content.ReadFromJsonAsync<SessionBody>();
        Assert.NotNull(newSession);
        Assert.NotEqual(session.AccessToken, newSession.AccessToken);

        // A sessão é recuperada por outra instância da API, a partir do PostgreSQL.
        await using var restarted = new DatabaseApplication(_mysql.ConnectionString);
        using var restored = restarted.CreateClient();
        restored.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", newSession.AccessToken);
        Assert.Equal(session.User.Id, (await restored.GetFromJsonAsync<UserBody>("/auth/me"))?.Id);

        var duplicate = await client.PostAsJsonAsync("/auth/register", new { name = "Outra Conta", email = email.ToUpperInvariant(), password = "OutraSenha2026!" });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    private sealed record UserBody(string Id, string Name, string Email);
    private sealed record SessionBody(string AccessToken, DateTimeOffset ExpiresAt, UserBody User);

    [PostgresFact]
    [Trait("Category", "Postgres")]
    public async Task Sessions_identify_only_their_owner_and_expired_sessions_are_rejected()
    {
        await using var application = new DatabaseApplication(_mysql.ConnectionString);
        using var client = application.CreateClient();
        await application.Services.GetRequiredService<PostgresDatabase>().MigrateAsync();
        async Task<SessionBody> Register(string name)
        {
            var response = await client.PostAsJsonAsync("/auth/register", new { name, email = $"{Guid.NewGuid():N}@example.test", password = "Treino2026!seguro" });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<SessionBody>())!;
        }
        var first = await Register("Primeiro Aluno");
        var second = await Register("Segundo Aluno");
        Assert.NotEqual(first.User.Id, second.User.Id);
        client.DefaultRequestHeaders.Authorization = new("Bearer", first.AccessToken);
        Assert.Equal(first.User.Id, (await client.GetFromJsonAsync<UserBody>($"/auth/me?userId={second.User.Id}"))?.Id);
        client.DefaultRequestHeaders.Authorization = new("Bearer", second.AccessToken);
        Assert.Equal(second.User.Id, (await client.GetFromJsonAsync<UserBody>("/auth/me"))?.Id);
        client.DefaultRequestHeaders.Authorization = new("Bearer", new string('x', 43));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/auth/me")).StatusCode);

        await using var later = application.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton<TimeProvider>(new LaterTime())));
        using var expired = later.CreateClient();
        expired.DefaultRequestHeaders.Authorization = new("Bearer", first.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await expired.GetAsync("/auth/me")).StatusCode);
    }

    private sealed class LaterTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UtcNow.AddDays(8);
    }
}
