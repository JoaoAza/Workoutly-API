using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Workoutly.Api.Auth;

public sealed class SessionAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, AuthService auth)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public static string? ReadToken(HttpRequest request) =>
        AuthenticationHeaderValue.TryParse(request.Headers.Authorization, out var header) &&
        string.Equals(header.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase) ? header.Parameter : null;

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Rotas públicas (inclusive /health) não devem depender do banco por um header antigo.
        if (Context.GetEndpoint()?.Metadata.GetMetadata<IAuthorizeData>() is null)
            return AuthenticateResult.NoResult();
        var token = ReadToken(Request);
        if (string.IsNullOrWhiteSpace(token))
            return AuthenticateResult.NoResult();
        var user = await auth.GetCurrentAsync(token, Context.RequestAborted);
        if (user is null)
            return AuthenticateResult.Fail("Sessão inválida ou expirada.");
        var identity = new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Name, user.Name),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role)
        ], Scheme.Name);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name));
    }
}
