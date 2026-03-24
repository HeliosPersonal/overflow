using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Overflow.IntegrationTests.Auth;

/// <summary>
/// Fake authentication handler that allows integration tests to run without Keycloak.
/// Set the "X-Test-UserId" header on HTTP requests to simulate an authenticated user.
/// </summary>
public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "TestScheme";
    public const string UserIdHeader = "X-Test-UserId";
    public const string UserRolesHeader = "X-Test-Roles";

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(UserIdHeader, out var userIdValues))
            return Task.FromResult(AuthenticateResult.NoResult());

        var userId = userIdValues.ToString();
        if (string.IsNullOrEmpty(userId))
            return Task.FromResult(AuthenticateResult.NoResult());

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Name, $"TestUser-{userId}")
        };

        // Optionally add role claims
        if (Request.Headers.TryGetValue(UserRolesHeader, out var rolesValues))
        {
            foreach (var role in rolesValues.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                claims.Add(new Claim(ClaimTypes.Role, role.Trim()));
            }
        }

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

/// <summary>
/// Extension methods for <see cref="HttpClient"/> to set test auth headers.
/// </summary>
public static class HttpClientTestAuthExtensions
{
    public static void SetTestUser(this HttpClient client, string userId, params string[] roles)
    {
        client.DefaultRequestHeaders.Remove(TestAuthHandler.UserIdHeader);
        client.DefaultRequestHeaders.Remove(TestAuthHandler.UserRolesHeader);
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId);
        if (roles.Length > 0)
            client.DefaultRequestHeaders.Add(TestAuthHandler.UserRolesHeader, string.Join(",", roles));
    }

    public static void ClearTestUser(this HttpClient client)
    {
        client.DefaultRequestHeaders.Remove(TestAuthHandler.UserIdHeader);
        client.DefaultRequestHeaders.Remove(TestAuthHandler.UserRolesHeader);
    }
}