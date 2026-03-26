using Microsoft.Extensions.Options;
using Overflow.DataSeederService.Clients;
using Overflow.DataSeederService.Keycloak;
using Overflow.DataSeederService.Models;

namespace Overflow.DataSeederService.Services;

/// <summary>
///     Singleton that holds the AI user account. Bootstrapped on startup by <see cref="AiUserBootstrapService" />.
///     Provides token refresh for posting answers.
/// </summary>
public class AiUserProvider(
    KeycloakAdminService keycloakAdmin,
    IProfileApiClient profileApi,
    IOptions<AiAnswerOptions> options,
    ILogger<AiUserProvider> logger)
{
    private readonly AiAnswerOptions _options = options.Value;
    private readonly TaskCompletionSource<AiUser> _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private AiUser? _user;

    /// <summary>Waits until the AI user is bootstrapped. Returns null if bootstrap failed.</summary>
    public async Task<AiUser?> GetUserAsync(CancellationToken ct = default)
    {
        try
        {
            return await _ready.Task.WaitAsync(ct);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    ///     Ensures the AI Keycloak account exists, authenticates it, and triggers profile creation.
    ///     Called once on startup by <see cref="AiUserBootstrapService" />.
    /// </summary>
    public async Task BootstrapAsync(CancellationToken ct = default)
    {
        logger.LogInformation("Bootstrapping AI user '{DisplayName}' ({Email})",
            _options.AiDisplayName, _options.AiEmail);

        // Create or find the Keycloak account
        var nameParts = _options.AiDisplayName.Split(' ', 2);
        var firstName = nameParts[0];
        var lastName = nameParts.Length > 1 ? nameParts[1] : "Bot";

        var userId = await keycloakAdmin.CreateUserAsync(
            _options.AiEmail, firstName, lastName, _options.AiPassword, ct);

        if (userId == null)
        {
            logger.LogError("Failed to create/find AI user in Keycloak");
            throw new InvalidOperationException("Cannot bootstrap AI user — Keycloak account creation failed");
        }

        // Authenticate to get a token
        var token = await keycloakAdmin.GetUserTokenAsync(_options.AiEmail, _options.AiPassword, ct);
        if (token == null)
        {
            logger.LogError("AI user created in Keycloak but authentication failed");
            throw new InvalidOperationException("Cannot bootstrap AI user — authentication failed");
        }

        // Trigger profile auto-creation
        try
        {
            await profileApi.GetMyProfileAsync($"Bearer {token}", ct);
            logger.LogInformation("AI user profile ensured");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AI user profile auto-creation request failed (will retry on next answer)");
        }

        _user = new AiUser
        {
            KeycloakUserId = userId,
            Email = _options.AiEmail,
            Token = token
        };

        _ready.TrySetResult(_user);
        logger.LogInformation("AI user bootstrapped — ID: {UserId}, Email: {Email}", userId, _options.AiEmail);
    }

    /// <summary>Returns a fresh token for the AI user, refreshing if needed.</summary>
    public async Task<string?> GetFreshTokenAsync(CancellationToken ct = default)
    {
        if (_user == null) return null;

        var token = await keycloakAdmin.GetUserTokenAsync(_user.Email, _options.AiPassword, ct);
        if (token != null)
        {
            _user.Token = token;
        }
        else
        {
            logger.LogWarning("Failed to refresh AI user token");
        }

        return token;
    }
}