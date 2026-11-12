using Npgsql;
using NpgsqlTypes;

namespace Workoutly.Api.Data;

internal static class PostgresParameters
{
    // A API expõe IDs como strings; o PostgreSQL recebe UUID, inclusive em filtros nulos.
    public static void AddUuid(this NpgsqlParameterCollection parameters, string name, string? value) =>
        parameters.AddWithValue(name, NpgsqlDbType.Uuid,
            value is null ? DBNull.Value : Guid.Parse(value));
}
