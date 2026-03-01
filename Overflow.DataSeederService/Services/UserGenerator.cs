using Bogus;
using Microsoft.Extensions.Options;
using Overflow.DataSeederService.Models;

namespace Overflow.DataSeederService.Services;

public class UserGenerator
{
    private readonly HttpClient _httpClient;
    private readonly SeederOptions _options;
    private readonly AuthenticationService _authService;
    private readonly KeycloakAdminService _keycloakAdminService;
    private readonly ILogger<UserGenerator> _logger;
    private readonly Faker _faker;

    public UserGenerator(
        HttpClient httpClient, 
        IOptions<SeederOptions> options,
        AuthenticationService authService,
        KeycloakAdminService keycloakAdminService,
        ILogger<UserGenerator> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _authService = authService;
        _keycloakAdminService = keycloakAdminService;
        _logger = logger;
        _faker = new Faker();
    }

    /// <summary>
    /// Manages a fixed pool of seeder users (default 20).
    /// On each cycle: discovers existing seeder-* users in Keycloak, rehydrates them
    /// (reset password + get token), and creates new ones only if under the limit.
    /// Non-seeder users in the system are never touched.
    /// </summary>
    public async Task<List<UserProfileWithAuth>> GetOrCreateUserPoolAsync(CancellationToken cancellationToken = default)
    {
        var maxUsers = _options.MaxSeederUsers;
        var prefix = _options.SeederUsernamePrefix;

        try
        {
            // Step 1: Discover existing seeder users in Keycloak
            _logger.LogInformation("[UserPool] Searching for existing seeder users (prefix: '{Prefix}')...", prefix);
            var existingSeederUsers = await _keycloakAdminService.SearchUsersByPrefixAsync(
                prefix, maxUsers, cancellationToken);
            _logger.LogInformation("[UserPool] Found {Count} existing seeder users in Keycloak", existingSeederUsers.Count);

            var userPool = new List<UserProfileWithAuth>();

            // Step 2: Rehydrate existing seeder users (reset password + get fresh tokens)
            foreach (var (keycloakUserId, username) in existingSeederUsers)
            {
                if (userPool.Count >= maxUsers) break;

                var (userId, token) = await _authService.RehydrateExistingUserAsync(
                    keycloakUserId, username, cancellationToken);

                if (userId != null && token != null)
                {
                    // Trigger profile middleware to ensure profile exists
                    await TriggerProfileMiddlewareAsync(token, cancellationToken);

                    userPool.Add(new UserProfileWithAuth
                    {
                        Profile = new UserProfile
                        {
                            Id = keycloakUserId,
                            DisplayName = username
                        },
                        KeycloakUserId = keycloakUserId,
                        Token = token
                    });
                    _logger.LogDebug("[UserPool] Rehydrated: {Username} ({UserId})", username, keycloakUserId);
                }
                else
                {
                    _logger.LogWarning("[UserPool] Failed to rehydrate: {Username} ({UserId})", username, keycloakUserId);
                }

                // Small delay between rehydrations
                await Task.Delay(Random.Shared.Next(50, 200), cancellationToken);
            }

            _logger.LogInformation("[UserPool] Rehydrated {Count}/{Found} existing seeder users", 
                userPool.Count, existingSeederUsers.Count);

            // Step 3: Create new seeder users if under the limit
            var usersToCreate = maxUsers - userPool.Count;
            if (usersToCreate > 0)
            {
                _logger.LogInformation("[UserPool] Creating {Count} new seeder users to reach limit of {Max}...", 
                    usersToCreate, maxUsers);

                for (int i = 0; i < usersToCreate; i++)
                {
                    var user = await CreateRandomUserAsync(cancellationToken);
                    if (user != null)
                    {
                        userPool.Add(user);
                    }
                    // Small delay between user creation
                    await Task.Delay(Random.Shared.Next(100, 500), cancellationToken);
                }
            }

            _logger.LogInformation("[UserPool] 📊 Pool ready: {Count}/{Max} seeder users", 
                userPool.Count, maxUsers);

            return userPool;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[UserPool] ❌ Error managing user pool");
            
            // Fallback: create a few new users
            _logger.LogWarning("[UserPool] Falling back: creating 3 new seeder users");
            var fallbackUsers = await CreateMultipleUsersAsync(3, cancellationToken);
            return fallbackUsers;
        }
    }

    /// <summary>
    /// Create a user: Keycloak first, then trigger profile auto-creation via middleware.
    /// </summary>
    public async Task<UserProfileWithAuth?> CreateRandomUserAsync(CancellationToken cancellationToken = default)
    {
        // Generate realistic display name
        var displayName = _faker.Name.FullName();

        try
        {
            _logger.LogDebug("[UserGen] Creating Keycloak user: {DisplayName}", displayName);
            
            // Create user in Keycloak (source of truth)
            var (keycloakUserId, token) = await _authService.CreateKeycloakUserAndGetTokenAsync(
                displayName, 
                cancellationToken);

            if (keycloakUserId == null || token == null)
            {
                _logger.LogError("[UserGen] ❌ Failed to create Keycloak user: {DisplayName}", displayName);
                return null;
            }

            // Trigger UserProfileCreationMiddleware by making an authenticated request
            await TriggerProfileMiddlewareAsync(token, cancellationToken);
            
            _logger.LogInformation("[UserGen] ✅ Created user: {DisplayName} (Keycloak ID: {KeycloakId})", 
                displayName, keycloakUserId);
            
            return new UserProfileWithAuth
            {
                Profile = new UserProfile
                {
                    Id = keycloakUserId,
                    DisplayName = displayName
                },
                KeycloakUserId = keycloakUserId,
                Token = token
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[UserGen] ❌ Error creating user: {DisplayName}", displayName);
            return null;
        }
    }

    private async Task TriggerProfileMiddlewareAsync(string token, CancellationToken cancellationToken)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{_options.ProfileServiceUrl}/profiles/me");
            request.Headers.Add("Authorization", $"Bearer {token}");
            await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[UserGen] Profile middleware trigger failed (non-critical)");
        }
    }

    public async Task<List<UserProfileWithAuth>> CreateMultipleUsersAsync(int count, CancellationToken cancellationToken = default)
    {
        var users = new List<UserProfileWithAuth>();

        for (int i = 0; i < count; i++)
        {
            var user = await CreateRandomUserAsync(cancellationToken);
            if (user != null)
            {
                users.Add(user);
                // Small delay between user creation
                await Task.Delay(Random.Shared.Next(100, 500), cancellationToken);
            }
        }

        return users;
    }
}
