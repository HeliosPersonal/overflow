using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Overflow.ProfileService.Services;

/// <summary>
/// Lightweight Keycloak Admin REST API client for ProfileService.
/// Used by <see cref="AnonymousUserCleanupService"/> to find and delete stale anonymous users.
/// </summary>
public class KeycloakAdminClient(HttpClient http, ILogger<KeycloakAdminClient> logger)
{
    /// <summary>
    /// Email domain used for anonymous (guest) user placeholder emails.
    /// Must match the constant in <c>webapp/src/lib/keycloak-admin.ts</c>.
    /// </summary>
    public const string AnonymousEmailDomain = "@anonymous.overflow.local";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private string? _token;
    private DateTime _tokenExpiry = DateTime.MinValue;

    /// <summary>
    /// Acquires an admin access token via client_credentials grant.
    /// Caches the token until it expires.
    /// </summary>
    public async Task AuthenticateAsync(string tokenUrl, string clientId, string clientSecret,
        CancellationToken ct = default)
    {
        if (_token is not null && DateTime.UtcNow < _tokenExpiry.AddMinutes(-1))
            return;

        var response = await http.PostAsync(tokenUrl, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
        }), ct);

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(ct);
        var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(json, JsonOptions);

        _token = tokenResponse?.AccessToken
                 ?? throw new InvalidOperationException("Keycloak token response missing access_token");
        _tokenExpiry = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);
        logger.LogDebug("Obtained Keycloak admin token (expires in {Seconds}s)", tokenResponse.ExpiresIn);
    }

    /// <summary>
    /// Searches for users by email domain. Keycloak's search is a prefix/contains match
    /// so we search for the anonymous domain and filter client-side for exact domain match.
    /// </summary>
    public async Task<List<KeycloakUser>> FindAnonymousUsersAsync(string adminBaseUrl, CancellationToken ct = default)
    {
        var url = $"{adminBaseUrl}/users?search={Uri.EscapeDataString(AnonymousEmailDomain)}&max=1000";
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);

        var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        var users = JsonSerializer.Deserialize<List<KeycloakUser>>(json, JsonOptions) ?? [];

        // Filter to exact anonymous domain match (Keycloak search is fuzzy)
        return users
            .Where(u => u.Email?.EndsWith(AnonymousEmailDomain, StringComparison.OrdinalIgnoreCase) == true)
            .ToList();
    }

    /// <summary>
    /// Deletes a user from Keycloak by user ID.
    /// </summary>
    public async Task DeleteUserAsync(string adminBaseUrl, string userId, CancellationToken ct = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, $"{adminBaseUrl}/users/{userId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);

        var response = await http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            logger.LogWarning("Failed to delete Keycloak user {UserId}: {Status} {Body}",
                userId, response.StatusCode, body);
        }
    }

    public record KeycloakUser
    {
        public string Id { get; init; } = string.Empty;
        public string? Username { get; init; }
        public string? Email { get; init; }
        public string? FirstName { get; init; }
        public string? LastName { get; init; }

        /// <summary>Keycloak creation timestamp in milliseconds since epoch.</summary>
        public long CreatedTimestamp { get; init; }

        public DateTime CreatedAtUtc => DateTimeOffset.FromUnixTimeMilliseconds(CreatedTimestamp).UtcDateTime;
    }

    private record TokenResponse
    {
        [JsonPropertyName("access_token")] public string? AccessToken { get; init; }
        [JsonPropertyName("expires_in")] public int ExpiresIn { get; init; }
    }
}