using CommandFlow;
using Microsoft.EntityFrameworkCore;
using Overflow.EstimationService.Clients;
using Overflow.EstimationService.Data;
using Overflow.EstimationService.Services;

namespace Overflow.EstimationService.Features.Rooms.Commands;

public record InvalidateProfileCacheCommand(string UserId) : ICommand;

public class InvalidateProfileCacheHandler(
    ProfileServiceClient profileClient,
    EstimationDbContext db,
    CrossPodBroadcastService crossPodBroadcast,
    ILogger<InvalidateProfileCacheHandler> logger)
    : ICommandHandler<InvalidateProfileCacheCommand>
{
    public async Task HandleCommand(InvalidateProfileCacheCommand request, CancellationToken cancellationToken)
    {
        // Evict the cached profile so subsequent fetches get fresh data
        await profileClient.InvalidateAsync(request.UserId);

        // Find all rooms where this user is a participant
        var roomIds = await db.Participants
            .Where(p => p.UserId == request.UserId)
            .Select(p => p.RoomId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (roomIds.Count == 0)
        {
            logger.LogDebug("User {UserId} has no active rooms, skipping broadcast", request.UserId);
            return;
        }

        // Broadcast room updates to all affected rooms so WebSocket clients see the fresh avatar immediately
        logger.LogInformation(
            "Broadcasting avatar update for user {UserId} across {Count} room(s)",
            request.UserId, roomIds.Count);

        foreach (var roomId in roomIds)
        {
            try
            {
                await crossPodBroadcast.PublishRoomUpdateAsync(roomId);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to broadcast room update for {RoomId}", roomId);
            }
        }
    }
}