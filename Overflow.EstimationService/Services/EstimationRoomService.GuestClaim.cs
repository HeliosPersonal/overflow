using Microsoft.EntityFrameworkCore;

namespace Overflow.EstimationService.Services;

public partial class EstimationRoomService
{
    public async Task<int> ClaimGuestAsync(string guestId, string userId, string displayName)
    {
        var guestParticipants = await db.Participants
            .Where(p => p.GuestId == guestId)
            .ToListAsync();

        if (guestParticipants.Count == 0) return 0;

        var affectedRoomIds = new List<Guid>();
        var claimed = 0;

        foreach (var participant in guestParticipants)
        {
            var existingAuth = await db.Participants
                .FirstOrDefaultAsync(p => p.RoomId == participant.RoomId && p.UserId == userId);

            if (existingAuth is not null)
                await MergeGuestIntoExistingParticipant(participant, userId);
            else
                UpgradeGuestToAuthenticatedUser(participant, userId, displayName);

            affectedRoomIds.Add(participant.RoomId);
            claimed++;
        }

        await db.SaveChangesAsync();

        foreach (var roomId in affectedRoomIds.Distinct())
            await BroadcastRoomUpdateAsync(roomId);

        if (claimed > 0)
            logger.LogInformation("Claimed {Count} room(s) for guest {GuestId} → user {UserId}",
                claimed, guestId, userId);

        return claimed;
    }

    private async Task MergeGuestIntoExistingParticipant(
        Models.EstimationParticipant guestParticipant, string userId)
    {
        var guestVotes = await db.Votes
            .Where(v => v.RoomId == guestParticipant.RoomId && v.ParticipantId == guestParticipant.GuestId)
            .ToListAsync();

        foreach (var vote in guestVotes)
        {
            var hasExistingVote = await db.Votes
                .AnyAsync(v => v.RoomId == guestParticipant.RoomId &&
                               v.ParticipantId == userId && v.RoundNumber == vote.RoundNumber);
            if (!hasExistingVote)
                vote.ParticipantId = userId;
            else
                db.Votes.Remove(vote);
        }

        db.Participants.Remove(guestParticipant);
    }

    private static void UpgradeGuestToAuthenticatedUser(
        Models.EstimationParticipant participant, string userId, string displayName)
    {
        participant.ParticipantId = userId;
        participant.UserId = userId;
        participant.GuestId = null;
        participant.DisplayName = displayName;
        participant.IsGuest = false;
    }
}