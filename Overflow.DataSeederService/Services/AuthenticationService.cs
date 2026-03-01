using Microsoft.Extensions.Options;
using Overflow.DataSeederService.Models;

namespace Overflow.DataSeederService.Services;

/// <summary>
/// Authentication service for DataSeeder.
/// Creates real Keycloak users (source of truth) and obtains their access tokens.
/// </summary>
public class AuthenticationService
{
    private readonly KeycloakAdminService _keycloakAdminService;
    private readonly SeederOptions _options;
    private readonly ILogger<AuthenticationService> _logger;
    private readonly Dictionary<string, (string keycloakUserId, string username, string password)> _userCredentials = new();

    public AuthenticationService(
        KeycloakAdminService keycloakAdminService,
        IOptions<SeederOptions> options,
        ILogger<AuthenticationService> logger)
    {
        _keycloakAdminService = keycloakAdminService;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Re-adopt an existing Keycloak seeder user by resetting their password and obtaining a fresh token.
    /// </summary>
    public async Task<(string? keycloakUserId, string? token)> RehydrateExistingUserAsync(
        string keycloakUserId, string username, CancellationToken cancellationToken = default)
    {
        // Check if already cached
        if (_userCredentials.TryGetValue(keycloakUserId, out var cached))
        {
            var existingToken = await _keycloakAdminService.GetUserTokenAsync(
                cached.username, cached.password, cancellationToken);
            if (existingToken != null)
                return (keycloakUserId, existingToken);
        }

        // Reset password so we can authenticate as this user
        var newPassword = GeneratePassword();
        var resetSuccess = await _keycloakAdminService.ResetUserPasswordAsync(
            keycloakUserId, newPassword, cancellationToken);

        if (!resetSuccess)
        {
            _logger.LogWarning("Failed to reset password for existing seeder user {Username} ({UserId})", 
                username, keycloakUserId);
            return (keycloakUserId, null);
        }

        // Cache credentials
        _userCredentials[keycloakUserId] = (keycloakUserId, username, newPassword);

        // Get token
        var token = await _keycloakAdminService.GetUserTokenAsync(username, newPassword, cancellationToken);

        if (token == null)
        {
            _logger.LogWarning("Failed to obtain token for rehydrated user {Username}", username);
            return (keycloakUserId, null);
        }

        _logger.LogInformation("Rehydrated existing seeder user {Username} ({UserId})", username, keycloakUserId);
        return (keycloakUserId, token);
    }

    /// <summary>
    /// Create a new Keycloak user and get their access token.
    /// Returns the Keycloak user ID and token that can be used to create a profile.
    /// </summary>
    public async Task<(string? keycloakUserId, string? token)> CreateKeycloakUserAndGetTokenAsync(
        string displayName, 
        CancellationToken cancellationToken = default)
    {
        // Generate username and password
        var username = GenerateUsername(displayName);
        var password = GeneratePassword();

        // Check if we already created this user (by display name)
        var existingUser = _userCredentials.Values.FirstOrDefault(u => 
            u.username.Equals(username, StringComparison.OrdinalIgnoreCase));
        
        if (existingUser != default)
        {
            var token = await _keycloakAdminService.GetUserTokenAsync(
                existingUser.username, 
                existingUser.password, 
                cancellationToken);
            return (existingUser.keycloakUserId, token);
        }

        // Create user in Keycloak
        var keycloakUserId = await _keycloakAdminService.CreateUserAsync(
            username,
            $"{username}@overflow.local",
            displayName.Split(' ').FirstOrDefault() ?? displayName,
            displayName.Split(' ').Skip(1).FirstOrDefault() ?? "",
            password,
            cancellationToken);

        if (keycloakUserId == null)
        {
            _logger.LogError("Failed to create Keycloak user for {DisplayName}", displayName);
            return (null, null);
        }

        // Cache credentials
        _userCredentials[keycloakUserId] = (keycloakUserId, username, password);

        // Get token for this user
        var userToken = await _keycloakAdminService.GetUserTokenAsync(username, password, cancellationToken);

        if (userToken == null)
        {
            _logger.LogError("Failed to obtain token for user {Username}", username);
            return (keycloakUserId, null);
        }

        _logger.LogInformation("Created Keycloak user {Username} ({DisplayName}) with ID {KeycloakUserId}", 
            username, displayName, keycloakUserId);

        return (keycloakUserId, userToken);
    }

    /// <summary>
    /// Get an existing user's token by their Keycloak user ID.
    /// </summary>
    public async Task<string?> GetUserTokenAsync(string keycloakUserId, CancellationToken cancellationToken = default)
    {
        if (!_userCredentials.TryGetValue(keycloakUserId, out var credentials))
        {
            _logger.LogWarning("No cached credentials for Keycloak user {UserId}", keycloakUserId);
            return null;
        }

        return await _keycloakAdminService.GetUserTokenAsync(
            credentials.username, 
            credentials.password, 
            cancellationToken);
    }

    private string GenerateUsername(string displayName)
    {
        // Create a username from display name with seeder prefix and random suffix
        var namePart = displayName
            .ToLowerInvariant()
            .Replace(" ", "")
            .Replace(".", "")
            .Replace("'", "")
            .Replace("-", "");

        // Take first 10 characters of name and add random suffix
        var namePrefix = namePart.Length > 10 ? namePart.Substring(0, 10) : namePart;
        var randomSuffix = Random.Shared.Next(1000, 9999);

        return $"{_options.SeederUsernamePrefix}{namePrefix}{randomSuffix}";
    }

    private string GeneratePassword()
    {
        // Generate a random password
        return Guid.NewGuid().ToString("N");
    }
}


