using Microsoft.EntityFrameworkCore;
using Overflow.EstimationService.Data;
using Overflow.EstimationService.Models;
using ZiggyCreatures.Caching.Fusion;

namespace Overflow.EstimationService.Services;

/// <summary>
/// Centralized cache layer for estimation rooms using FusionCache (L1 in-memory + L2 Redis).
/// Provides cache-aside pattern: reads go through cache, writes invalidate.
/// The Redis backplane ensures all pods see invalidations immediately.
/// </summary>
public class RoomCacheService(
    IFusionCache cache,
    IServiceScopeFactory scopeFactory,
    ILogger<RoomCacheService> logger)
{
    private static string RoomKey(Guid roomId) => $"room:{roomId}";
    private static string UserRoomsKey(string userId) => $"user-rooms:{userId}";

    /// <summary>
    /// Default cache duration for active room state.
    /// Short duration because rooms are mutated frequently during voting.
    /// FusionCache's fail-safe will serve stale data if the DB is momentarily unreachable.
    /// </summary>
    private static readonly TimeSpan RoomCacheDuration = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Cache duration for user room lists (less frequently accessed).
    /// </summary>
    private static readonly TimeSpan UserRoomsCacheDuration = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Gets a room with all navigation properties, using cache-aside pattern.
    /// </summary>
    public async Task<EstimationRoom?> GetRoomAsync(Guid roomId, CancellationToken ct = default)
    {
        return await cache.GetOrSetAsync<EstimationRoom?>(
            RoomKey(roomId),
            async (ctx, token) =>
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<EstimationDbContext>();
                var room = await db.Rooms
                    .Include(r => r.Participants)
                    .Include(r => r.Votes)
                    .Include(r => r.RoundHistory)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(r => r.Id == roomId, token);

                if (room is null)
                {
                    // Short-circuit: cache null for a short time to prevent repeated DB misses
                    ctx.Options.Duration = TimeSpan.FromSeconds(5);
                }

                return room;
            },
            new FusionCacheEntryOptions(RoomCacheDuration)
            {
                // Serve stale data while refreshing in background (eager refresh)
                EagerRefreshThreshold = 0.8f,
                // Allow stale data for up to 5 minutes if DB is down
                FailSafeMaxDuration = TimeSpan.FromMinutes(5),
                IsFailSafeEnabled = true,
            },
            ct
        );
    }

    /// <summary>
    /// Gets rooms for a user, using cache-aside pattern.
    /// </summary>
    public async Task<IReadOnlyList<EstimationRoom>> GetRoomsForUserAsync(string userId,
        CancellationToken ct = default)
    {
        var result = await cache.GetOrSetAsync<IReadOnlyList<EstimationRoom>>(
            UserRoomsKey(userId),
            async (_, token) =>
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<EstimationDbContext>();
                return await db.Rooms
                    .Include(r => r.Participants)
                    .Include(r => r.RoundHistory)
                    .AsNoTracking()
                    .Where(r => r.Participants.Any(p => p.UserId == userId))
                    .OrderByDescending(r => r.UpdatedAtUtc)
                    .ToListAsync(token);
            },
            new FusionCacheEntryOptions(UserRoomsCacheDuration)
            {
                IsFailSafeEnabled = true,
                FailSafeMaxDuration = TimeSpan.FromMinutes(10),
            },
            ct
        );

        return result;
    }

    /// <summary>
    /// Invalidates room cache after a mutation. The Redis backplane propagates
    /// this to all pods so their L1 caches are cleared too.
    /// </summary>
    public async Task InvalidateRoomAsync(Guid roomId, CancellationToken ct = default)
    {
        logger.LogDebug("Invalidating cache for room {RoomId}", roomId);
        await cache.RemoveAsync(RoomKey(roomId), token: ct);
    }

    /// <summary>
    /// Invalidates user room list cache.
    /// </summary>
    public async Task InvalidateUserRoomsAsync(string userId, CancellationToken ct = default)
    {
        await cache.RemoveAsync(UserRoomsKey(userId), token: ct);
    }

    /// <summary>
    /// Invalidates room cache and all affected user caches after a mutation.
    /// </summary>
    public async Task InvalidateRoomAndUsersAsync(Guid roomId, IEnumerable<string?> userIds,
        CancellationToken ct = default)
    {
        await InvalidateRoomAsync(roomId, ct);
        foreach (var userId in userIds.Where(u => u is not null).Distinct())
        {
            await InvalidateUserRoomsAsync(userId!, ct);
        }
    }
}