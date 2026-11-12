using System.Net;
using System.Net.Http.Json;

namespace Workoutly.Api.Tests;

public sealed class HealthEndpointTests
{
    [Fact]
    public async Task Health_reports_api_available_without_claiming_database_connectivity()
    {
        await using var application = new DatabaseApplication(null);
        using var client = application.CreateClient();
        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadFromJsonAsync<HealthResponse>();
        Assert.NotNull(body);
        Assert.Equal("Workoutly.Api", body.Service);
        Assert.Equal("ok", body.Status);
        Assert.False(body.DatabaseConfigured);
    }

    private sealed record HealthResponse(string Service, string Status, bool DatabaseConfigured);
}
