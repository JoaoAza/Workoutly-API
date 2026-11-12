using Npgsql;

namespace Workoutly.Api.Data;

public sealed class PostgresDatabase(IConfiguration configuration)
{
    public const int CurrentSchemaVersion = 9;
    private string? ConnectionString => configuration.GetConnectionString("Workoutly") is { Length: > 0 } value
        ? value : configuration["DATABASE_URL"];
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ConnectionString);

    public async Task<DatabaseReadiness> CheckReadinessAsync(CancellationToken cancellationToken)
    {
        if (!IsConfigured) return new(false, "not_configured", null);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(3));
        try
        {
            await using var connection = await OpenConnectionAsync(timeout.Token);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT MAX(version) FROM schema_migrations";
            var value = await command.ExecuteScalarAsync(timeout.Token);
            int? version = value is null or DBNull ? null : Convert.ToInt32(value);
            return new(version == CurrentSchemaVersion, version == CurrentSchemaVersion ? "ready" : "schema_pending", version);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UndefinedTable)
        { return new(false, "schema_pending", null); }
        catch (NpgsqlException) { return new(false, "unreachable", null); }
        catch (Exception exception) when (exception is ArgumentException or FormatException)
        { return new(false, "invalid_configuration", null); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { return new(false, "unreachable", null); }
    }

    // Somente --migrate: não altera banco automaticamente no startup.
    // O banco versão 9 já preparado pelo pgAdmin é aceito sem reescrever dados.
    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT pg_advisory_xact_lock(17412026)";
        await command.ExecuteNonQueryAsync(cancellationToken);
        command.CommandText = "SELECT to_regclass('public.schema_migrations') IS NOT NULL";
        if ((bool)(await command.ExecuteScalarAsync(cancellationToken))!)
        {
            command.CommandText = "SELECT MAX(version) FROM schema_migrations";
            var version = await command.ExecuteScalarAsync(cancellationToken);
            if (version is not int number || number != CurrentSchemaVersion)
                throw new InvalidOperationException("Esquema existente incompleto ou incompatível. Confira as migrações antes de continuar.");
            await transaction.CommitAsync(cancellationToken);
            return;
        }
        var assembly = typeof(PostgresDatabase).Assembly;
        foreach (var resource in assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith("Workoutly.Api.Data.Migrations.") && name.EndsWith(".sql"))
            .Order(StringComparer.Ordinal))
        {
            await using var stream = assembly.GetManifestResourceStream(resource)!;
            using var reader = new StreamReader(stream);
            command.CommandText = await reader.ReadToEndAsync(cancellationToken);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<NpgsqlConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var raw = ConnectionString;
        if (string.IsNullOrWhiteSpace(raw))
            throw new InvalidOperationException("Defina DATABASE_URL ou ConnectionStrings__Workoutly no servidor.");
        NpgsqlConnectionStringBuilder options;
        if (raw.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) || raw.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            var uri = new Uri(raw);
            var credentials = uri.UserInfo.Split(':', 2);
            if (credentials.Length != 2 || string.IsNullOrWhiteSpace(uri.Host) || uri.AbsolutePath.Length < 2)
                throw new ArgumentException("URL PostgreSQL inválida.");
            options = new()
            {
                Host = uri.Host,
                Port = uri.IsDefaultPort ? 5432 : uri.Port,
                Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
                Username = Uri.UnescapeDataString(credentials[0]),
                Password = Uri.UnescapeDataString(credentials[1]),
                SslMode = SslMode.Require
            };
            // As URLs internas/externas do Render são aceitas; TLS nunca é desativado pela query.
        }
        else options = new(raw);
        if (string.IsNullOrWhiteSpace(options.Database)) throw new ArgumentException("Informe o banco de dados.");
        options.PersistSecurityInfo = false;
        options.IncludeErrorDetail = false;
        options.Timeout = 3;
        options.CommandTimeout = 15;
        if (!options.ContainsKey("Maximum Pool Size")) options.MaxPoolSize = 20;
        var connection = new NpgsqlConnection(options.ConnectionString);
        try { await connection.OpenAsync(cancellationToken); return connection; }
        catch { await connection.DisposeAsync(); throw; }
    }
}

public sealed record DatabaseReadiness(bool Ready, string Database, int? SchemaVersion);
