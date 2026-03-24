using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Overflow.EstimationService.Data;
using Overflow.EstimationService.Exceptions;
using Overflow.EstimationService.Models;

namespace Overflow.EstimationService.Services;

/// <summary>
/// Core room state management — mutations go through EF Core, then publish
/// cross-pod Redis broadcasts so all pods push fresh snapshots via WebSocket.
/// All reads go directly to the database (no caching layer).
/// All mutation queries use AsNoTracking to avoid concurrency conflicts with WebSocket disconnect handlers.
/// </summary>
public partial class EstimationRoomService(
    EstimationDbContext db,
    CrossPodBroadcastService crossPodBroadcast,
    ILogger<EstimationRoomService> logger)
{
    private const int MaxTitleLength = 80;

    public async Task<EstimationRoom?> GetRoomByIdAsync(Guid roomId)
    {
        return await db.Rooms
            .Include(r => r.Participants)
            .Include(r => r.Votes)
            .Include(r => r.RoundHistory)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == roomId);
    }

    public async Task<IReadOnlyList<EstimationRoom>> GetRoomsForUserAsync(string userId)
    {
        return await db.Rooms
            .Include(r => r.Participants)
            .Include(r => r.RoundHistory)
            .AsNoTracking()
            .Where(r => r.Participants.Any(p => p.UserId == userId))
            .OrderByDescending(r => r.UpdatedAtUtc)
            .ToListAsync();
    }

    /// <summary>Publishes cross-pod broadcast so all pods push fresh room state via WebSocket. Failures are logged but never propagated.</summary>
    private async Task BroadcastRoomUpdateAsync(Guid roomId)
    {
        try
        {
            await crossPodBroadcast.PublishRoomUpdateAsync(roomId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to publish cross-pod broadcast for room {RoomId}", roomId);
        }
    }

    /// <summary>Loads room with all includes as AsNoTracking snapshot.</summary>
    private async Task<Result<EstimationRoom, RoomError>> GetRoomWithAll(Guid roomId)
    {
        var room = await db.Rooms
            .Include(r => r.Participants)
            .Include(r => r.Votes)
            .Include(r => r.RoundHistory)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == roomId);

        return room is not null
            ? Result.Success<EstimationRoom, RoomError>(room)
            : RoomErrors.NotFound(roomId);
    }

    private async Task<Result<EstimationRoom, RoomError>> ReloadRoom(Guid roomId)
        => await GetRoomWithAll(roomId);

    private async Task TouchRoomAsync(Guid roomId)
    {
        var now = DateTime.UtcNow;
        await db.Rooms.Where(r => r.Id == roomId)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.UpdatedAtUtc, now));
    }

    private async Task<List<Guid>> UpdateDisplayNameAcrossRoomsAsync(string participantId, string newDisplayName)
    {
        var affectedRoomIds = await db.Participants
            .Where(p => p.ParticipantId == participantId && p.DisplayName != newDisplayName)
            .Select(p => p.RoomId)
            .Distinct()
            .ToListAsync();

        if (affectedRoomIds.Count == 0) return affectedRoomIds;

        await db.Participants
            .Where(p => p.ParticipantId == participantId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.DisplayName, newDisplayName));

        logger.LogInformation(
            "Updated display name for participant {ParticipantId} across {Count} room(s)",
            participantId, affectedRoomIds.Count);

        return affectedRoomIds;
    }

    private static UnitResult<RoomError> EnsureModerator(EstimationRoom room, string userId)
    {
        return room.ModeratorUserId == userId
            ? UnitResult.Success<RoomError>()
            : UnitResult.Failure(RoomErrors.NotModerator());
    }
}