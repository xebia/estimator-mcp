using System.Security.Claims;
using System.Text.Encodings.Web;
using EstimatorMcp.Web.Services.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace EstimatorMcp.Web.Auth;

public class BearerTokenAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder,
    IServiceScopeFactory scopeFactory)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, loggerFactory, encoder)
{
    public const string SchemeName = "BearerToken";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authHeader = Request.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return AuthenticateResult.NoResult();

        var rawToken = authHeader["Bearer ".Length..].Trim();
        if (string.IsNullOrEmpty(rawToken))
            return AuthenticateResult.Fail("Empty token");

        using var scope = scopeFactory.CreateScope();
        var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();
        var email = await tokenService.ValidateTokenAsync(rawToken);

        if (email is null)
            return AuthenticateResult.Fail("Invalid token");

        var claims = new[] { new Claim(ClaimTypes.Email, email), new Claim(ClaimTypes.Name, email) };
        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        return AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = 401;
        Response.Headers.WWWAuthenticate = $"Bearer realm=\"{Request.Host}\"";
        return Task.CompletedTask;
    }
}
