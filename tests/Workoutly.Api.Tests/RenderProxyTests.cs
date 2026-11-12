using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Workoutly.Api.Tests;

public sealed class RenderProxyTests
{
    [Theory]
    [InlineData(true, "https", HttpStatusCode.Unauthorized)]
    [InlineData(false, "https", HttpStatusCode.BadRequest)]
    [InlineData(true, "http", HttpStatusCode.BadRequest)]
    public async Task Production_accepts_forwarded_https_only_when_render_proxy_is_enabled(bool enabled, string proto, HttpStatusCode expected)
    {
        await using var app = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string,string?>
            {
                ["Hosting:RenderProxy"] = enabled.ToString(),
                ["ConnectionStrings:Workoutly"] = "",
                ["DATABASE_URL"] = ""
            }));
        });
        using var client = app.CreateClient(new() { BaseAddress = new Uri("http://localhost"), AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Forwarded-Proto", proto);
        Assert.Equal(expected, (await client.GetAsync("/auth/me")).StatusCode);
    }
}
