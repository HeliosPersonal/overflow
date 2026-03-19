using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Overflow.NotificationService.Auth;

/// <summary>
/// Authentication handler that validates an internal API key passed via
/// the <c>X-Api-Key</c> header. Used for server-to-server calls (e.g. webapp
/// forgot-password route) where no user JWT is available.
///
/// Registered alongside Keycloak JWT bearer auth so the <c>[Authorize]</c>
/// attribute accepts either mechanism.
/// </summary>
public class ApiKeyAuthenticationHandler(
    IOptionsMonitor<ApiKeyAuthenticationOptions> options,
    ILoggerFactory logger,
    System.Text.Encodings.Web.UrlEncoder encoder)
    : AuthenticationHandler<ApiKeyAuthenticationOptions>(options, logger, encoder)
{
    public const string SchemeName = "ApiKey";
    private const string ApiKeyHeaderName = "X-Api-Key";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ApiKeyHeaderName, out var providedKey))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var expectedKey = Options.ApiKey;
        if (string.IsNullOrEmpty(expectedKey))
        {
            Logger.LogWarning("Internal API key is not configured — rejecting X-Api-Key request");
            return Task.FromResult(AuthenticateResult.Fail("API key authentication is not configured"));
        }

        if (!string.Equals(providedKey, expectedKey, StringComparison.Ordinal))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key"));
        }

        var identity = new ClaimsIdentity(SchemeName);
        identity.AddClaim(new Claim(ClaimTypes.Name, "internal-service"));
        identity.AddClaim(new Claim("auth_method", "api_key"));

        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    /// <summary>
    /// The expected API key value. Set via configuration (<c>NOTIFICATION_INTERNAL_API_KEY</c>).
    /// </summary>
    public string? ApiKey { get; set; }
}