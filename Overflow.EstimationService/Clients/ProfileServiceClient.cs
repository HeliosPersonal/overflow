using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Overflow.EstimationService.Clients;

/// <summary>
/// HTTP client that fetches user display names from the Profile Service.
/// Includes an in-memory cache to avoid repeated calls for the same user.
/// </summary>
public class ProfileServiceClient(HttpClient http, ILogger<ProfileServiceClient> logger)
{
    // Simple in-memory cache: userId → displayName (TTL managed via sliding window)
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Gets the display name for a user from the Profile Service.
    /// Returns null if the profile cannot be fetched (caller should fall back to token claims).
    /// </summary>
    public async Task<string?> GetDisplayNameAsync(string userId, string? accessToken = null)
    {
        // Check cache first
        if (_cache.TryGetValue(userId, out var cached) && cached.ExpiresAtUtc > DateTime.UtcNow)
        {
            return cached.DisplayName;
        }

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"/profiles/{userId}");
            if (accessToken is not null)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            }

            var response = await http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogDebug("Profile fetch for {UserId} returned {Status}", userId, response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            var profile = JsonSerializer.Deserialize<ProfileResponse>(json, JsonOptions);
            var displayName = profile?.DisplayName;

            if (!string.IsNullOrWhiteSpace(displayName))
            {
                _cache[userId] = new CacheEntry(displayName, DateTime.UtcNow + CacheTtl);
            }

            return displayName;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch profile for {UserId}", userId);
            return null;
        }
    }

    private record CacheEntry(string DisplayName, DateTime ExpiresAtUtc);

    private record ProfileResponse(string Id, string DisplayName, int Reputation);
}