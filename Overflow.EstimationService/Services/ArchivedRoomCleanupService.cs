using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Overflow.EstimationService.Data;
using Overflow.EstimationService.Models;
using Overflow.EstimationService.Options;

namespace Overflow.EstimationService.Services;

/// <summary>
/// Periodically:
/// 1. Auto-archives rooms inactive for <see cref="RoomCleanupOptions.InactiveDaysBeforeArchive"/> days.
/// 2. Deletes archived rooms older than <see cref="RoomCleanupOptions.ArchivedDaysBeforeDelete"/> days.
/// </summary>
public class ArchivedRoomCleanupService(
    IServiceScopeFactory scopeFactory,
    CrossPodBroadcastService crossPodBroadcast,
    IOptions<RoomCleanupOptions> options,
    ILogger<ArchivedRoomCleanupService> logger) : BackgroundService
{
    private readonly RoomCleanupOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Room cleanup service started. InactiveDaysBeforeArchive={ArchiveDays}d, ArchivedDaysBeforeDelete={DeleteDays}d, Interval={IntervalHours}h",
            _options.InactiveDaysBeforeArchive, _options.ArchivedDaysBeforeDelete, _options.IntervalHours);

        // Short initial delay so the app can finish starting up
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var archived = await AutoArchiveStaleRoomsAsync(stoppingToken);
                if (archived > 0)
                    logger.LogInformation(
                        "Auto-archived {Count} stale room(s) inactive for {Days}+ days",
                        archived, _options.InactiveDaysBeforeArchive);

                var deleted = await DeleteExpiredArchivedRoomsAsync(stoppingToken);
                if (deleted > 0)
                    logger.LogInformation(
                        "Deleted {Count} archived room(s) archived for {Days}+ days",
                        deleted, _options.ArchivedDaysBeforeDelete);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Error during room cleanup");
            }

            await Task.Delay(TimeSpan.FromHours(_options.IntervalHours), stoppingToken);
        }
    }

    /// <summary>
    /// Auto-archives rooms that are not archived but haven't been updated within the inactivity threshold.
    /// </summary>
    private async Task<int> AutoArchiveStaleRoomsAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<EstimationDbContext>();

        var cutoff = DateTime.UtcNow.AddDays(-_options.InactiveDaysBeforeArchive);

        var staleRooms = await db.Rooms
            .Where(r => r.Status != RoomStatus.Archived && r.UpdatedAtUtc < cutoff)
            .Select(r => new { r.Id, r.Title, r.UpdatedAtUtc })
            .ToListAsync(ct);

        if (staleRooms.Count == 0) return 0;

        var now = DateTime.UtcNow;
        var staleRoomIds = staleRooms.Select(r => r.Id).ToList();

        foreach (var room in staleRooms)
            logger.LogInformation(
                "Auto-archiving stale room {RoomId} ({Title}) — last updated {LastUpdated:u}",
                room.Id, room.Title, room.UpdatedAtUtc);

        var archived = await db.Rooms
            .Where(r => staleRoomIds.Contains(r.Id))
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Status, RoomStatus.Archived)
                .SetProperty(r => r.ArchivedAtUtc, now)
                .SetProperty(r => r.UpdatedAtUtc, now), ct);

        // Broadcast to any still-connected WebSocket clients so they see the archive status
        foreach (var room in staleRooms)
        {
            try
            {
                await crossPodBroadcast.PublishRoomUpdateAsync(room.Id);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to broadcast archive for room {RoomId}", room.Id);
            }
        }

        return archived;
    }

    /// <summary>
    /// Deletes archived rooms that have been archived for longer than the configured threshold.
    /// </summary>
    private async Task<int> DeleteExpiredArchivedRoomsAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<EstimationDbContext>();

        var cutoff = DateTime.UtcNow.AddDays(-_options.ArchivedDaysBeforeDelete);

        // Find archived rooms past the retention window
        var expiredRooms = await db.Rooms
            .Where(r => r.Status == RoomStatus.Archived && r.ArchivedAtUtc != null && r.ArchivedAtUtc < cutoff)
            .Select(r => new { r.Id, r.Title })
            .ToListAsync(ct);

        if (expiredRooms.Count == 0)
            return 0;

        var expiredRoomIds = expiredRooms.Select(r => r.Id).ToList();

        foreach (var room in expiredRooms)
            logger.LogInformation("Deleting archived room {RoomId} ({RoomTitle})", room.Id, room.Title);

        // Delete related entities first (votes, round history, participants), then the rooms.
        // Using ExecuteDeleteAsync for efficient bulk deletion without loading entities.
        await db.Votes
            .Where(v => expiredRoomIds.Contains(v.RoomId))
            .ExecuteDeleteAsync(ct);

        await db.RoundHistory
            .Where(h => expiredRoomIds.Contains(h.RoomId))
            .ExecuteDeleteAsync(ct);

        await db.Participants
            .Where(p => expiredRoomIds.Contains(p.RoomId))
            .ExecuteDeleteAsync(ct);

        var deleted = await db.Rooms
            .Where(r => expiredRoomIds.Contains(r.Id))
            .ExecuteDeleteAsync(ct);

        return deleted;
    }
}