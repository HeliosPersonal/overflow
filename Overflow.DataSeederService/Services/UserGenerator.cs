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
