using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Overflow.Common.Options;

namespace Overflow.DataSeederService.Services;

/// <summary>
/// Service for managing Keycloak users via Admin API.
/// Creates real users and obtains their access tokens.
/// </summary>
public class KeycloakAdminService
{
    private readonly HttpClient _httpClient;
    private readonly KeycloakOptions _keycloakOptions;
    private readonly ILogger<KeycloakAdminService> _logger;
    private string? _adminToken;
    private DateTime _adminTokenExpiry = DateTime.MinValue;

    public KeycloakAdminService(
        HttpClient httpClient,
        IOptions<KeycloakOptions> keycloakOptions,
        ILogger<KeycloakAdminService> logger)
    {
        _httpClient = httpClient;
        _keycloakOptions = keycloakOptions.Value;
        _logger = logger;
    }

    /// <summary>
    /// Get admin token for Keycloak Admin API operations.
    /// </summary>
    private async Task<string?> GetAdminTokenAsync(CancellationToken cancellationToken = default)
    {
        // Check if we have a cached token that's still valid
        if (!string.IsNullOrEmpty(_adminToken) && DateTime.UtcNow < _adminTokenExpiry.AddMinutes(-1))
        {
            return _adminToken;
        }

        try
        {
            var tokenUrl = $"{_keycloakOptions.Url}/realms/{_keycloakOptions.Realm}/protocol/openid-connect/token";

            var requestBody = new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = _keycloakOptions.AdminClientId ?? throw new InvalidOperationException("AdminClientId is required"),
                ["client_secret"] = _keycloakOptions.AdminClientSecret ?? throw new InvalidOperationException("AdminClientSecret is required")
            };

            var response = await _httpClient.PostAsync(
                tokenUrl,
                new FormUrlEncodedContent(requestBody),
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Failed to obtain admin token: {StatusCode} - {Error}",
                    response.StatusCode, error);
                return null;
            }

            var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken);

            if (tokenResponse?.AccessToken == null)
            {
                _logger.LogError("Admin token response did not contain an access token");
                return null;
            }

            _adminToken = tokenResponse.AccessToken;
            _adminTokenExpiry = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);

            _logger.LogDebug("Successfully obtained admin token (expires in {Seconds}s)", tokenResponse.ExpiresIn);

            return _adminToken;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obtaining admin token");
            return null;
        }
    }

    /// <summary>
    /// Create a new user in Keycloak and return the user ID.
    /// </summary>
    public async Task<string?> CreateUserAsync(string username, string email, string firstName, string lastName, string password, CancellationToken cancellationToken = default)
    {
        var adminToken = await GetAdminTokenAsync(cancellationToken);
        if (adminToken == null)
        {
            _logger.LogError("Cannot create user without admin token");
            return null;
        }

        try
        {
            var createUserUrl = $"{_keycloakOptions.Url}/admin/realms/{_keycloakOptions.Realm}/users";

            var userRepresentation = new
            {
                username,
                email,
                firstName,
                lastName,
                enabled = true,
                emailVerified = true,
                credentials = new[]
                {
                    new
                    {
                        type = "password",
                        value = password,
                        temporary = false
                    }
                }
            };

            var request = new HttpRequestMessage(HttpMethod.Post, createUserUrl)
            {
                Content = JsonContent.Create(userRepresentation)
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                _logger.LogWarning("User {Username} already exists", username);
                // Try to get existing user ID
                return await GetUserIdByUsernameAsync(username, cancellationToken);
            }

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Failed to create user {Username}: {StatusCode} - {Error}",
                    username, response.StatusCode, error);
                return null;
            }

            // Extract user ID from Location header
            var locationHeader = response.Headers.Location?.ToString();
            if (locationHeader != null)
            {
                var userId = locationHeader.Split('/').Last();
                _logger.LogInformation("Created user {Username} with ID {UserId}", username, userId);
                return userId;
            }

            _logger.LogError("User created but no Location header returned");
            return await GetUserIdByUsernameAsync(username, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user {Username}", username);
            return null;
        }
    }

    /// <summary>
    /// Get user ID by username.
    /// </summary>
    private async Task<string?> GetUserIdByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        var adminToken = await GetAdminTokenAsync(cancellationToken);
        if (adminToken == null) return null;

        try
        {
            var getUserUrl = $"{_keycloakOptions.Url}/admin/realms/{_keycloakOptions.Realm}/users?username={username}&exact=true";

            var request = new HttpRequestMessage(HttpMethod.Get, getUserUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var users = await response.Content.ReadFromJsonAsync<List<KeycloakUser>>(cancellationToken);
            return users?.FirstOrDefault()?.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user ID for {Username}", username);
            return null;
        }
    }

    /// <summary>
    /// Obtain an access token for a specific user using password grant.
    /// </summary>
    public async Task<string?> GetUserTokenAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        try
        {
            var tokenUrl = $"{_keycloakOptions.Url}/realms/{_keycloakOptions.Realm}/protocol/openid-connect/token";

            var requestBody = new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["client_id"] = _keycloakOptions.NextJsClientId ?? throw new InvalidOperationException("NextJsClientId is required"),
                ["username"] = username,
                ["password"] = password
            };

            // Add client secret if available (for confidential clients)
            if (!string.IsNullOrEmpty(_keycloakOptions.NextJsClientSecret))
            {
                requestBody["client_secret"] = _keycloakOptions.NextJsClientSecret;
            }

            var response = await _httpClient.PostAsync(
                tokenUrl,
                new FormUrlEncodedContent(requestBody),
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Failed to obtain token for user {Username}: {StatusCode} - {Error}",
                    username, response.StatusCode, error);
                return null;
            }

            var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken);

            if (tokenResponse?.AccessToken == null)
            {
                _logger.LogError("Token response for user {Username} did not contain an access token", username);
                return null;
            }

            _logger.LogDebug("Successfully obtained token for user {Username}", username);

            return tokenResponse.AccessToken;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obtaining token for user {Username}", username);
            return null;
        }
    }

    private class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("token_type")]
        public string? TokenType { get; set; }
    }

    /// <summary>
    /// Search for Keycloak users whose username starts with the given prefix.
    /// </summary>
    public async Task<List<(string keycloakUserId, string username)>> SearchUsersByPrefixAsync(
        string usernamePrefix, int max = 100, CancellationToken cancellationToken = default)
    {
        var adminToken = await GetAdminTokenAsync(cancellationToken);
        if (adminToken == null) return new List<(string, string)>();

        try
        {
            var searchUrl = $"{_keycloakOptions.Url}/admin/realms/{_keycloakOptions.Realm}/users?search={usernamePrefix}&max={max}";

            var request = new HttpRequestMessage(HttpMethod.Get, searchUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to search users by prefix '{Prefix}': {StatusCode}", 
                    usernamePrefix, response.StatusCode);
                return new List<(string, string)>();
            }

            var users = await response.Content.ReadFromJsonAsync<List<KeycloakUser>>(cancellationToken);
            return users?
                .Where(u => u.Id != null && u.Username != null && u.Username.StartsWith(usernamePrefix))
                .Select(u => (u.Id!, u.Username!))
                .ToList() ?? new List<(string, string)>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching users by prefix '{Prefix}'", usernamePrefix);
            return new List<(string, string)>();
        }
    }

    /// <summary>
    /// Reset a user's password in Keycloak so the seeder can re-authenticate as them.
    /// </summary>
    public async Task<bool> ResetUserPasswordAsync(string keycloakUserId, string newPassword, 
        CancellationToken cancellationToken = default)
    {
        var adminToken = await GetAdminTokenAsync(cancellationToken);
        if (adminToken == null) return false;

        try
        {
            var resetUrl = $"{_keycloakOptions.Url}/admin/realms/{_keycloakOptions.Realm}/users/{keycloakUserId}/reset-password";

            var credential = new
            {
                type = "password",
                value = newPassword,
                temporary = false
            };

            var request = new HttpRequestMessage(HttpMethod.Put, resetUrl)
            {
                Content = JsonContent.Create(credential)
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to reset password for user {UserId}: {StatusCode}", 
                    keycloakUserId, response.StatusCode);
                return false;
            }

            _logger.LogDebug("Reset password for user {UserId}", keycloakUserId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting password for user {UserId}", keycloakUserId);
            return false;
        }
    }

    private class KeycloakUser
    {
        [JsonPropertyName("id")]
        public string? Id { get; init; }

        [JsonPropertyName("username")]
        public string? Username { get; init; }
    }
}
