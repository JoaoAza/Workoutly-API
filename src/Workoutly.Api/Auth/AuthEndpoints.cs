using System.Net.Mail;
using System.Security.Claims;

namespace Workoutly.Api.Auth;

public static class AuthEndpoints
{
    public static void MapAccountEndpoints(this WebApplication app)
    {
        var accounts = app.MapGroup("/auth");
        accounts.MapPost("/register", async (RegisterRequest request, AuthService auth, CancellationToken cancellationToken) =>
        {
            var errors = Validate(request.Email, request.Password, registration: true);
            if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length is < 2 or > 120)
                errors["name"] = ["Informe um nome entre 2 e 120 caracteres."];
            var role = request.Role is null ? "student" : request.Role.Trim().ToLowerInvariant();
            if (role is not ("student" or "teacher"))
                errors["role"] = ["Escolha o perfil de aluno ou professor."];
            if (errors.Count > 0)
                return Results.ValidationProblem(errors);
            var session = await auth.RegisterAsync(request.Name!, request.Email!, request.Password!, role, cancellationToken);
            return session is null ? Results.Conflict(new { message = "Este e-mail já está cadastrado." })
                : Results.Json(session, statusCode: StatusCodes.Status201Created);
        }).RequireRateLimiting("account");

        accounts.MapPost("/login", async (LoginRequest request, AuthService auth, CancellationToken cancellationToken) =>
        {
            var errors = Validate(request.Email, request.Password, registration: false);
            if (errors.Count > 0)
                return Results.ValidationProblem(errors);
            var session = await auth.LoginAsync(request.Email!, request.Password!, cancellationToken);
            return session is null ? Results.Json(new { message = "E-mail ou senha inválidos." }, statusCode: StatusCodes.Status401Unauthorized)
                : Results.Ok(session);
        }).RequireRateLimiting("account");

        accounts.MapGet("/me", (ClaimsPrincipal user) => Results.Ok(new AccountProfile(
            user.FindFirstValue(ClaimTypes.NameIdentifier)!, user.FindFirstValue(ClaimTypes.Name)!, user.FindFirstValue(ClaimTypes.Email)!, user.FindFirstValue(ClaimTypes.Role)!)))
            .RequireAuthorization();

        accounts.MapPost("/logout", async (HttpRequest request, AuthService auth, CancellationToken cancellationToken) =>
        {
            await auth.RevokeAsync(SessionAuthenticationHandler.ReadToken(request)!, cancellationToken);
            return Results.NoContent();
        }).RequireAuthorization();
    }

    internal static Dictionary<string, string[]> Validate(string? email, string? password, bool registration)
    {
        var errors = new Dictionary<string, string[]>();
        var address = email?.Trim();
        if (string.IsNullOrWhiteSpace(address) || address.Length > 254 ||
            !MailAddress.TryCreate(address, out var parsed) || !string.Equals(parsed.Address, address, StringComparison.OrdinalIgnoreCase))
            errors["email"] = ["Informe um e-mail válido com até 254 caracteres."];
        if (string.IsNullOrWhiteSpace(password) || password.Length > 128 || (registration && password.Length < 10))
            errors["password"] = [registration ? "Use uma senha entre 10 e 128 caracteres." : "Informe sua senha (até 128 caracteres)."];
        return errors;
    }

    public sealed record RegisterRequest(string? Name, string? Email, string? Password, string? Role);
    public sealed record LoginRequest(string? Email, string? Password);
}
