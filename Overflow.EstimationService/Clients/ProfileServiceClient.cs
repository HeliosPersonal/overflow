using System.Net.Http.Headers;
using System.Text.Json;
using ZiggyCreatures.Caching.Fusion;

namespace Overflow.EstimationService.Clients;

/// <summary>
/// HTTP client that fetches user display names from the Profile Service.
/// Uses FusionCache (L1 in-memory + L2 Redis) so display names are shared
/// across pods and survive restarts.
/// </summary>
public class ProfileServiceClient(
    HttpClient http,
    IFusionCache cache,
    ILogger<ProfileServiceClient> logger)
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(60);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static string CacheKey(string userId) => $"profile:{userId}";

    /// <summary>
    /// Gets the profile data (display name + avatar URL) for a user from the Profile Service.
    /// Returns null if the profile cannot be fetched (caller should fall back to token claims).
    /// </summary>
    public async Task<ProfileData?> GetProfileDataAsync(string userId, string? accessToken = null)
    {
        return await cache.GetOrSetAsync<ProfileData?>(
            CacheKey(userId),
            async (_, ct) =>
            {
                try
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, $"/profiles/{userId}");
                    if (accessToken is not null)
                    {
                        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                    }

                    var response = await http.SendAsync(request, ct);
                    if (!response.IsSuccessStatusCode)
                    {
                        logger.LogDebug("Profile fetch for {UserId} returned {Status}", userId,
                            response.StatusCode);
                        return null;
                    }

                    var json = await response.Content.ReadAsStringAsync(ct);
                    var profile = JsonSerializer.Deserialize<ProfileResponse>(json, JsonOptions);
                    return profile is not null
                        ? new ProfileData(profile.DisplayName, profile.AvatarUrl)
                        : null;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to fetch profile for {UserId}", userId);
                    return null;
                }
            },
            new FusionCacheEntryOptions(CacheDuration)
            {
                IsFailSafeEnabled = true,
                FailSafeMaxDuration = TimeSpan.FromMinutes(5),
            }
        );
    }

    /// <summary>
    /// Gets the display name for a user from the Profile Service.
    /// Returns null if the profile cannot be fetched (caller should fall back to token claims).
    /// </summary>
    public async Task<string?> GetDisplayNameAsync(string userId, string? accessToken = null)
    {
        var data = await GetProfileDataAsync(userId, accessToken);
        return data?.DisplayName;
    }

    /// <summary>
    /// Evicts the cached profile for the given user so the next call
    /// to <see cref="GetProfileDataAsync"/> fetches fresh data from ProfileService.
    /// The FusionCache backplane propagates this eviction to all pods.
    /// </summary>
    public async Task InvalidateAsync(string userId)
    {
        await cache.RemoveAsync(CacheKey(userId));
        logger.LogDebug("Invalidated profile cache for {UserId}", userId);
    }

    public record ProfileData(string DisplayName, string? AvatarUrl);

    private record ProfileResponse(string UserId, string DisplayName, int Reputation, string? AvatarUrl);
}