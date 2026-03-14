using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Overflow.EstimationService.Data;
using Overflow.EstimationService.Models;
using Overflow.EstimationService.Options;

namespace Overflow.EstimationService.Services;

/// <summary>
/// Periodically deletes archived rooms that exceed the configured retention period.
/// Removes the room along with all related participants, votes, and round history.
/// </summary>
public class ArchivedRoomCleanupService(
    IServiceScopeFactory scopeFactory,
    IOptions<RoomCleanupOptions> options,
    ILogger<ArchivedRoomCleanupService> logger) : BackgroundService
{
    private readonly RoomCleanupOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Archived room cleanup service started. Retention={RetentionDays}d, Interval={IntervalHours}h",
            _options.RetentionDays, _options.IntervalHours);

        // Short initial delay so the app can finish starting up
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var deleted = await CleanupAsync(stoppingToken);
                if (deleted > 0)
                    logger.LogInformation("Deleted {Count} archived room(s) older than {Days} days", deleted,
                        _options.RetentionDays);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Error during archived room cleanup");
            }

            await Task.Delay(TimeSpan.FromHours(_options.IntervalHours), stoppingToken);
        }
    }

    private async Task<int> CleanupAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<EstimationDbContext>();

        var cutoff = DateTime.UtcNow.AddDays(-_options.RetentionDays);

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