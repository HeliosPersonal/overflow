using Bogus;
using Microsoft.Extensions.Options;
using Overflow.DataSeederService.Models;
using System.Net.Http.Json;

namespace Overflow.DataSeederService.Services;

public class UserGenerator
{
    private readonly HttpClient _httpClient;
    private readonly SeederOptions _options;
    private readonly AuthenticationService _authService;
    private readonly ILogger<UserGenerator> _logger;
    private readonly Faker _faker;
    private const int MaxUserPoolSize = 1000;
    private const int MinUsersForSeeding = 10;

    public UserGenerator(
        HttpClient httpClient, 
        IOptions<SeederOptions> options,
        AuthenticationService authService,
        ILogger<UserGenerator> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _authService = authService;
        _logger = logger;
        _faker = new Faker();
    }

    /// <summary>
    /// Smart user pool management: maintains a pool of up to 1000 users.
    /// Always creates fresh users for each seeding cycle to ensure valid authentication tokens.
    /// </summary>
    public async Task<List<UserProfileWithAuth>> GetOrCreateUserPoolAsync(CancellationToken cancellationToken = default)
    {

        try
        {
            // Fetch existing profiles from Profile Service to check pool size
            var response = await _httpClient.GetAsync(
                $"{_options.ProfileServiceUrl}/profiles?take={MaxUserPoolSize}", 
                cancellationToken);

            int existingProfileCount = 0;
            
            if (response.IsSuccessStatusCode)
            {
                var existingProfiles = await response.Content.ReadFromJsonAsync<List<UserProfile>>(cancellationToken) 
                                   ?? new List<UserProfile>();
                existingProfileCount = existingProfiles.Count;
                _logger.LogInformation("[UserPool] Found {Count} existing profiles", existingProfileCount);
            }
            else
            {
                _logger.LogWarning("[UserPool] Failed to fetch profiles: {StatusCode}", response.StatusCode);
            }

            var userPool = new List<UserProfileWithAuth>();

            // Calculate how many new users we need to create
            int currentPoolSize = existingProfileCount;
            int usersNeeded = 0;

            if (currentPoolSize < MinUsersForSeeding)
            {
                // We need at least MinUsersForSeeding users to start
                usersNeeded = MinUsersForSeeding - currentPoolSize;
                _logger.LogInformation("[UserPool] Pool too small ({Current} users), creating {ToCreate} users to reach minimum", 
                    currentPoolSize, usersNeeded);
            }
            else if (currentPoolSize < MaxUserPoolSize)
            {
                // Gradually grow the pool - add 5-20 users each time until we reach max
                usersNeeded = Random.Shared.Next(5, Math.Min(21, MaxUserPoolSize - currentPoolSize + 1));
                _logger.LogInformation("[UserPool] Growing pool: {Current} → {Target} (max {Max})", 
                    currentPoolSize, currentPoolSize + usersNeeded, MaxUserPoolSize);
            }
            else
            {
                _logger.LogInformation("[UserPool] At maximum ({Count}/{Max}), creating fresh users for cycle", 
                    currentPoolSize, MaxUserPoolSize);
                // Even at max capacity, create a few fresh users for seeding with valid tokens
                usersNeeded = Random.Shared.Next(3, 8);
            }

            // Always create fresh users for the current seeding cycle
            if (usersNeeded > 0)
            {
                _logger.LogInformation("[UserPool] Creating {Count} fresh users...", usersNeeded);
                var newUsers = await CreateMultipleUsersAsync(usersNeeded, cancellationToken);
                userPool.AddRange(newUsers);
                _logger.LogInformation("[UserPool] ✅ Created {Count}/{Needed} users successfully", newUsers.Count, usersNeeded);
            }

            _logger.LogInformation("[UserPool] 📊 Ready: {Created} users | System total: {Total}/1000", 
                userPool.Count, currentPoolSize + userPool.Count);

            return userPool;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[UserPool] ❌ Error managing user pool");
            
            // Fallback: create minimum required users
            _logger.LogWarning("[UserPool] Falling back: creating {Count} new users", MinUsersForSeeding);
            var fallbackUsers = await CreateMultipleUsersAsync(MinUsersForSeeding, cancellationToken);
            
            return fallbackUsers;
        }
    }

    /// <summary>
    /// Create a user: Keycloak first, then let middleware auto-create profile on first auth.
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

            _logger.LogInformation("[UserGen] ✅ Created user: {DisplayName} (Keycloak ID: {KeycloakId})", 
                displayName, keycloakUserId);

            // Profile will be auto-created by UserProfileCreationMiddleware on first authenticated request
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
