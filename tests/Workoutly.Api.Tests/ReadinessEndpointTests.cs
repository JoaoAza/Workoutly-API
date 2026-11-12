using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Workoutly.Api.Tests;

public sealed class ReadinessEndpointTests
{
    [Theory]
    [InlineData(null, "not_configured")]
    [InlineData("Host=127.0.0.1;Port=1;Username=invalid;Password=do-not-expose;Database=invalid;Timeout=1", "unreachable")]
    [InlineData("invalid-connection-string-do-not-expose", "invalid_configuration")]
    public async Task Readiness_returns_503_without_exposing_connection_details(string? connectionString, string reason)
    {
        await using var application = new DatabaseApplication(connectionString);
        using var client = application.CreateClient();

        var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ReadinessResponse>();
        Assert.NotNull(body);
        Assert.Equal("unavailable", body.Status);
        Assert.Equal(reason, body.Database);
        Assert.DoesNotContain("do-not-expose", await response.Content.ReadAsStringAsync());

        // Uma falha de banco não faz o processo da API deixar de estar vivo.
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
    }

    internal sealed record ReadinessResponse(string Status, string Database);
}

internal sealed class DatabaseApplication(string? connectionString) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Workoutly"] = connectionString
            }));
}
