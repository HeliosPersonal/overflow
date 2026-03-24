using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Overflow.EstimationService.Exceptions;
using Overflow.EstimationService.Models;

namespace Overflow.EstimationService.Services;

public partial class EstimationRoomService
{
    public async Task<Result<EstimationRoom, RoomError>> SubmitVoteAsync(
        Guid roomId, string participantId, string value)
    {
        var roomResult = await GetRoomWithAll(roomId);
        if (roomResult.IsFailure) return roomResult.Error;

        var room = roomResult.Value;

        if (room.Status != RoomStatus.Voting)
            return RoomErrors.InvalidState("Can only vote during Voting status");

        var participant = room.Participants.FirstOrDefault(p => p.ParticipantId == participantId);
        if (participant is null)
            return RoomErrors.ParticipantNotFound(participantId);
        if (participant.IsSpectator)
            return RoomErrors.SpectatorCannotVote();

        var deckValues = Decks.GetOrDefault(room.DeckType).Values;
        if (!deckValues.Contains(value))
            return RoomErrors.InvalidVote(value);

        await db.Votes
            .Where(v => v.RoomId == roomId && v.ParticipantId == participantId && v.RoundNumber == room.RoundNumber)
            .ExecuteDeleteAsync();

        db.Votes.Add(new EstimationVote
        {
            Id = Guid.NewGuid(),
            RoomId = roomId,
            RoundNumber = room.RoundNumber,
            ParticipantId = participantId,
            Value = value,
            SubmittedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        await TouchRoomAsync(roomId);

        await BroadcastRoomUpdateAsync(roomId);
        logger.LogDebug("Vote submitted by {ParticipantId} in room {RoomId}", participantId, roomId);
        return await ReloadRoom(roomId);
    }

    public async Task<UnitResult<RoomError>> ClearVoteAsync(Guid roomId, string participantId)
    {
        var roomResult = await GetRoomWithAll(roomId);
        if (roomResult.IsFailure) return roomResult.Error;

        var room = roomResult.Value;

        if (room.Status != RoomStatus.Voting)
            return RoomErrors.InvalidState("Can only clear vote during Voting status");

        var deleted = await db.Votes
            .Where(v => v.RoomId == roomId && v.ParticipantId == participantId && v.RoundNumber == room.RoundNumber)
            .ExecuteDeleteAsync();

        if (deleted > 0)
            await TouchRoomAsync(roomId);

        await BroadcastRoomUpdateAsync(roomId);
        logger.LogDebug("Vote cleared by {ParticipantId} in room {RoomId}", participantId, roomId);
        return UnitResult.Success<RoomError>();
    }
}