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
    private List<UserProfileWithAuth>? _cachedUserPool;

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
    /// Reuses existing users from the profile service and only creates new ones when needed.
    /// </summary>
    public async Task<List<UserProfileWithAuth>> GetOrCreateUserPoolAsync(CancellationToken cancellationToken = default)
    {
        // If we have cached pool and it's reasonably sized, return it
        if (_cachedUserPool != null && _cachedUserPool.Count >= MinUsersForSeeding)
        {
            _logger.LogInformation("Using cached user pool with {Count} users", _cachedUserPool.Count);
            return _cachedUserPool;
        }

        try
        {
            // Fetch existing profiles from Profile Service
            var response = await _httpClient.GetAsync(
                $"{_options.ProfileServiceUrl}/profiles?take={MaxUserPoolSize}", 
                cancellationToken);

            List<UserProfile> existingProfiles = new();
            
            if (response.IsSuccessStatusCode)
            {
                existingProfiles = await response.Content.ReadFromJsonAsync<List<UserProfile>>(cancellationToken) 
                                   ?? new List<UserProfile>();
                _logger.LogInformation("Found {Count} existing profiles in Profile Service", existingProfiles.Count);
            }

            var userPool = new List<UserProfileWithAuth>();

            // Create Keycloak users + tokens for existing profiles (up to 100 to avoid overwhelming the system)
            var profilesToAuth = existingProfiles.Take(100).ToList();
            foreach (var profile in profilesToAuth)
            {
                var (keycloakUserId, token) = await _authService.CreateKeycloakUserAndGetTokenAsync(
                    profile.DisplayName, 
                    cancellationToken);

                userPool.Add(new UserProfileWithAuth
                {
                    Profile = profile,
                    KeycloakUserId = keycloakUserId ?? profile.Id,
                    Token = token
                });
            }

            // Calculate how many new users we need to create
            int currentPoolSize = existingProfiles.Count;
            int usersNeeded = 0;

            if (currentPoolSize < MinUsersForSeeding)
            {
                // We need at least MinUsersForSeeding users to start
                usersNeeded = MinUsersForSeeding - currentPoolSize;
                _logger.LogInformation("Pool too small ({Current} users), creating {ToCreate} new users to reach minimum", 
                    currentPoolSize, usersNeeded);
            }
            else if (currentPoolSize < MaxUserPoolSize)
            {
                // Gradually grow the pool - add 5-20 users each time until we reach max
                usersNeeded = Random.Shared.Next(5, Math.Min(21, MaxUserPoolSize - currentPoolSize + 1));
                _logger.LogInformation("Growing user pool from {Current} to {Target} (max {Max})", 
                    currentPoolSize, currentPoolSize + usersNeeded, MaxUserPoolSize);
            }
            else
            {
                _logger.LogInformation("User pool at maximum capacity ({Count}/{Max}), reusing existing users", 
                    currentPoolSize, MaxUserPoolSize);
            }

            // Create new users if needed
            if (usersNeeded > 0)
            {
                _logger.LogInformation("Creating {Count} new users...", usersNeeded);
                var newUsers = await CreateMultipleUsersAsync(usersNeeded, cancellationToken);
                userPool.AddRange(newUsers);
                _logger.LogInformation("Successfully created {Count} new users", newUsers.Count);
            }

            // Cache the pool for this seeding cycle
            _cachedUserPool = userPool;

            _logger.LogInformation("User pool ready with {Count} users ({Existing} existing + {New} new)", 
                userPool.Count, profilesToAuth.Count, usersNeeded);

            return userPool;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error managing user pool");
            
            // Fallback: create minimum required users
            if (_cachedUserPool == null || _cachedUserPool.Count < MinUsersForSeeding)
            {
                _logger.LogWarning("Falling back to creating {Count} new users", MinUsersForSeeding);
                _cachedUserPool = await CreateMultipleUsersAsync(MinUsersForSeeding, cancellationToken);
            }
            
            return _cachedUserPool;
        }
    }

    /// <summary>
    /// Create a user: Keycloak first, then Profile.
    /// Keycloak is the source of truth.
    /// </summary>
    public async Task<UserProfileWithAuth?> CreateRandomUserAsync(CancellationToken cancellationToken = default)
    {
        // Generate realistic display name
        var displayName = _faker.Name.FullName();
        var description = _faker.Random.Bool(0.6f) ? _faker.Lorem.Sentence(10, 20) : null;

        try
        {
            // Step 1: Create user in Keycloak (source of truth)
            var (keycloakUserId, token) = await _authService.CreateKeycloakUserAndGetTokenAsync(
                displayName, 
                cancellationToken);

            if (keycloakUserId == null || token == null)
            {
                _logger.LogWarning("Failed to create Keycloak user for {DisplayName}", displayName);
                return null;
            }

            // Step 2: Create profile using authenticated request
            var profileDto = new CreateProfileDto
            {
                DisplayName = displayName,
                Description = description
            };

            var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.ProfileServiceUrl}/profiles");
            request.Headers.Add("Authorization", $"Bearer {token}");
            request.Content = JsonContent.Create(profileDto);

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var profile = await response.Content.ReadFromJsonAsync<UserProfile>(cancellationToken);
                if (profile != null)
                {
                    _logger.LogInformation("Created profile for {DisplayName} (Keycloak ID: {KeycloakId}, Profile ID: {ProfileId})", 
                        displayName, keycloakUserId, profile.Id);
                    
                    return new UserProfileWithAuth
                    {
                        Profile = profile,
                        KeycloakUserId = keycloakUserId,
                        Token = token
                    };
                }
            }

            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("Failed to create profile for Keycloak user {KeycloakId}: {StatusCode} - {Error}", 
                keycloakUserId, response.StatusCode, errorContent);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user {DisplayName}", displayName);
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

    /// <summary>
    /// Get existing profiles and create Keycloak users + tokens for them if needed.
    /// Note: This assumes profiles may exist without Keycloak users (legacy data).
    /// For new flow, all users should be created via CreateRandomUserAsync.
    /// </summary>
    public async Task<List<UserProfileWithAuth>> GetExistingUsersAsync(int maxCount = 100, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"{_options.ProfileServiceUrl}/profiles?take={maxCount}", 
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var profiles = await response.Content.ReadFromJsonAsync<List<UserProfile>>(cancellationToken);
                if (profiles == null || profiles.Count == 0)
                {
                    return new List<UserProfileWithAuth>();
                }

                var usersWithAuth = new List<UserProfileWithAuth>();

                // For each existing profile, we need to create a Keycloak user
                // (This is mainly for backward compatibility or initial seeding)
                foreach (var profile in profiles.Take(10)) // Limit to avoid creating too many at once
                {
                    var (keycloakUserId, token) = await _authService.CreateKeycloakUserAndGetTokenAsync(
                        profile.DisplayName, 
                        cancellationToken);

                    usersWithAuth.Add(new UserProfileWithAuth
                    {
                        Profile = profile,
                        KeycloakUserId = keycloakUserId,
                        Token = token
                    });
                }

                return usersWithAuth;
            }

            _logger.LogWarning("Failed to fetch existing profiles: {StatusCode}", response.StatusCode);
            return new List<UserProfileWithAuth>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching existing profiles");
            return new List<UserProfileWithAuth>();
        }
    }
}
