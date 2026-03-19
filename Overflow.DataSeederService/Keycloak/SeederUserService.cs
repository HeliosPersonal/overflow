using Microsoft.Extensions.Options;
using Overflow.DataSeederService.Models;

namespace Overflow.DataSeederService.Keycloak;

/// <summary>Creates and authenticates seeder Keycloak users using a single fixed password (set once, never reset).</summary>
public class SeederUserService(
    KeycloakAdminService keycloakAdmin,
    IOptions<SeederOptions> options,
    ILogger<SeederUserService> logger)
{
    private readonly Dictionary<string, string> _emailByUserId = new();
    private readonly SeederOptions _options = options.Value;

    /// <summary>Authenticates an existing seeder user. Uses the fixed password — no reset.</summary>
    public async Task<(string userId, string? token)> AuthenticateExistingUserAsync(
        string keycloakUserId, string email, CancellationToken ct = default)
    {
        _emailByUserId[keycloakUserId] = email;

        var token = await keycloakAdmin.GetUserTokenAsync(email, _options.SeederUserPassword, ct);
        if (token == null)
        {
            logger.LogWarning("Could not authenticate existing seeder user {Email}", email);
        }

        return (keycloakUserId, token);
    }

    /// <summary>Creates a new seeder user with the fixed password. Returns existing user if already in Keycloak.</summary>
    public async Task<(string? userId, string? token)> CreateUserAsync(
        string displayName, CancellationToken ct = default)
    {
        var email = BuildEmail(displayName);

        // Guard against duplicate creation within the same process lifetime
        var existingId = _emailByUserId
            .FirstOrDefault(kv => kv.Value.Equals(email, StringComparison.OrdinalIgnoreCase)).Key;
        if (existingId != null)
        {
            return (existingId, await GetFreshTokenAsync(existingId, ct));
        }

        var parts = displayName.Split(' ', 2);
        var userId = await keycloakAdmin.CreateUserAsync(
            email, parts[0], parts.Length > 1 ? parts[1] : "",
            _options.SeederUserPassword, ct);

        if (userId == null)
        {
            logger.LogError("Failed to create Keycloak user for {DisplayName}", displayName);
            return (null, null);
        }

        _emailByUserId[userId] = email;

        var token = await keycloakAdmin.GetUserTokenAsync(email, _options.SeederUserPassword, ct);
        if (token == null)
        {
            logger.LogError("User {Email} created in Keycloak but token request failed", email);
        }

        return (userId, token);
    }

    /// <summary>Returns a fresh token for a known seeder user.</summary>
    public async Task<string?> GetFreshTokenAsync(string keycloakUserId, CancellationToken ct = default)
    {
        if (!_emailByUserId.TryGetValue(keycloakUserId, out var email))
        {
            logger.LogWarning("No cached email for Keycloak user {Id} — cannot refresh token", keycloakUserId);
            return null;
        }

        return await keycloakAdmin.GetUserTokenAsync(email, _options.SeederUserPassword, ct);
    }

    private string BuildEmail(string displayName)
    {
        var clean = displayName.ToLowerInvariant()
            .Replace(" ", "").Replace(".", "").Replace("'", "").Replace("-", "");
        var prefix = clean.Length > 10 ? clean[..10] : clean;
        return $"{_options.SeederUsernamePrefix}{prefix}{Random.Shared.Next(1000, 9999)}@overflow.local";
    }
}