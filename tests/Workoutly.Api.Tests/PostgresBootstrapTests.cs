using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Workoutly.Api.Data;

namespace Workoutly.Api.Tests;

public sealed class PostgresBootstrapTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _mysql;

    public PostgresBootstrapTests(PostgresFixture mysql) => _mysql = mysql;

    [PostgresFact]
    [Trait("Category", "Postgres")]
    public async Task Migration_persists_schema_version_across_api_instances_and_can_be_reapplied()
    {
        await using (var first = new DatabaseApplication(_mysql.ConnectionString))
        {
            using var client = first.CreateClient();
            var pending = await client.GetAsync("/health/ready");
            Assert.Equal(HttpStatusCode.ServiceUnavailable, pending.StatusCode);
            var pendingBody = await pending.Content.ReadFromJsonAsync<ReadinessBody>();
            Assert.Equal("schema_pending", pendingBody?.Database);

            var database = first.Services.GetRequiredService<PostgresDatabase>();
            await database.MigrateAsync();
            await database.MigrateAsync();
        }

        // Outra instância da API, sem reaplicar a migração: o estado precisa vir do PostgreSQL.
        await using var restarted = new DatabaseApplication(_mysql.ConnectionString);
        using var restartedClient = restarted.CreateClient();
        var ready = await restartedClient.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
        var body = await ready.Content.ReadFromJsonAsync<ReadinessBody>();
        Assert.NotNull(body);
        Assert.Equal("ready", body.Status);
        Assert.Equal("ready", body.Database);
        Assert.Equal(9, body.SchemaVersion);
    }

    private sealed record ReadinessBody(string Status, string Database, int? SchemaVersion);
}

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WORKOUTLY_TEST_ADMIN_CONNECTION")))
            Skip = "Requer PostgreSQL isolado. Consulte INICIAR-RENDER.md.";
    }
}

public sealed class PostgresFixture : IAsyncLifetime
{
    // Base aleatória e isolada. Nunca apontar os testes para o banco do Render.
    private readonly string _databaseName = $"workoutly_test_{Guid.NewGuid():N}";
    private string? _adminConnection;
    private bool _created;
    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        _adminConnection = Environment.GetEnvironmentVariable("WORKOUTLY_TEST_ADMIN_CONNECTION");
        if (string.IsNullOrWhiteSpace(_adminConnection)) return;
        var options = new NpgsqlConnectionStringBuilder(_adminConnection) { Pooling = false, Timeout = 3 };
        if (string.IsNullOrWhiteSpace(options.Database)) options.Database = "postgres";
        _adminConnection = options.ConnectionString;
        await using var connection = new NpgsqlConnection(_adminConnection);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE \"{_databaseName}\"";
        await command.ExecuteNonQueryAsync();
        _created = true;
        options.Database = _databaseName;
        ConnectionString = options.ConnectionString;
    }

    public async Task DisposeAsync()
    {
        if (!_created) return;
        await using var connection = new NpgsqlConnection(_adminConnection);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP DATABASE \"{_databaseName}\" WITH (FORCE)";
        await command.ExecuteNonQueryAsync();
    }
}
