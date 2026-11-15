using System.Security.Claims;
using Npgsql;
using NpgsqlTypes;
using Workoutly.Api.Data;

namespace Workoutly.Api.Auth;

internal static class StudentProfileEndpoints
{
    public static void Map(RouteGroupBuilder accounts)
    {
        var profile = accounts.MapGroup("/me/student-profile").RequireAuthorization("student");
        profile.MapGet("", async (ClaimsPrincipal user, PostgresDatabase database, CancellationToken ct) =>
        {
            await using var connection = await database.OpenConnectionAsync(ct);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT u.name, u.first_name, u.last_name, u.body_weight_kg, u.height_cm
                FROM users u JOIN account_roles r ON r.user_id=u.id
                WHERE u.id=@user AND r.role='student'
                """;
            command.Parameters.AddUuid("user", user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            return await ReadProfileAsync(command, ct);
        });
        profile.MapPut("", async (StudentProfileRequest request, ClaimsPrincipal user, PostgresDatabase database, CancellationToken ct) =>
        {
            var firstName = request.FirstName?.Trim() ?? "";
            var lastName = request.LastName?.Trim();
            var name = string.IsNullOrEmpty(lastName) ? firstName : $"{firstName} {lastName}";
            var errors = new Dictionary<string, string[]>();
            if (firstName.Length == 0 || name.Length is < 2 or > 120)
                errors["name"] = ["Informe um nome. Nome e sobrenome juntos devem ter de 2 a 120 caracteres."];
            if (request.BodyWeightKg is decimal weight && (weight is <= 0 or > 1000 || decimal.Round(weight, 2) != weight))
                errors["bodyWeightKg"] = ["Informe um peso maior que 0 até 1000 kg, com até duas casas decimais."];
            if (request.HeightCm is < 1 or > 300)
                errors["heightCm"] = ["Informe a altura em centímetros inteiros, de 1 a 300."];
            if (errors.Count > 0) return Results.ValidationProblem(errors);

            await using var connection = await database.OpenConnectionAsync(ct);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                UPDATE users u SET name=@name, first_name=@firstName, last_name=@lastName, body_weight_kg=@weight, height_cm=@height
                WHERE u.id=@user AND EXISTS (SELECT 1 FROM account_roles r WHERE r.user_id=u.id AND r.role='student')
                RETURNING u.name, u.first_name, u.last_name, u.body_weight_kg, u.height_cm
                """;
            command.Parameters.AddUuid("user", user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            command.Parameters.AddWithValue("name", name);
            command.Parameters.AddWithValue("firstName", firstName);
            command.Parameters.AddWithValue("lastName", NpgsqlDbType.Varchar, string.IsNullOrEmpty(lastName) ? DBNull.Value : lastName);
            command.Parameters.AddWithValue("weight", NpgsqlDbType.Numeric, (object?)request.BodyWeightKg ?? DBNull.Value);
            command.Parameters.AddWithValue("height", NpgsqlDbType.Integer, (object?)request.HeightCm ?? DBNull.Value);
            return await ReadProfileAsync(command, ct);
        });
    }

    private static async Task<IResult> ReadProfileAsync(NpgsqlCommand command, CancellationToken ct)
    {
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return Results.NotFound();

        var name = reader.GetString(0);
        var firstName = reader.IsDBNull(1) ? null : reader.GetString(1);
        var lastName = reader.IsDBNull(2) ? null : reader.GetString(2);
        var savedName = lastName is null ? firstName : $"{firstName} {lastName}";
        // Contas antigas, ou editadas pelo fluxo de conta, usam o nome atual sem adivinhar o sobrenome.
        if (savedName != name)
            (firstName, lastName) = (name, null);

        return Results.Ok(new StudentProfile(name, firstName, lastName,
            reader.IsDBNull(3) ? null : reader.GetDecimal(3), reader.IsDBNull(4) ? null : reader.GetInt32(4)));
    }

    private sealed record StudentProfile(string Name, string? FirstName, string? LastName, decimal? BodyWeightKg, int? HeightCm);
    internal sealed record StudentProfileRequest(string? FirstName, string? LastName, decimal? BodyWeightKg, int? HeightCm);
}
