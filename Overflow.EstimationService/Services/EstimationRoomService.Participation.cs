using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Overflow.EstimationService.Exceptions;
using Overflow.EstimationService.Models;

namespace Overflow.EstimationService.Services;

public partial class EstimationRoomService
{
    public async Task<Result<EstimationRoom, RoomError>> CreateRoomAsync(
        string title, string moderatorParticipantId, string? moderatorUserId,
        string? moderatorGuestId, string moderatorDisplayName, bool isGuest, string? deckType,
        List<string>? tasks)
    {
        var deck = Decks.GetOrDefault(deckType);
        var now = DateTime.UtcNow;
        var tasksJson = TaskListHelper.SerializeTasks(tasks);

        var room = new EstimationRoom
        {
            Id = Guid.NewGuid(),
            Title = title.Trim(),
            ModeratorUserId = moderatorParticipantId,
            DeckType = deck.Id,
            Status = RoomStatus.Voting,
            RoundNumber = 1,
            TasksJson = tasksJson,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            Participants =
            [
                new EstimationParticipant
                {
                    Id = Guid.NewGuid(),
                    ParticipantId = moderatorParticipantId,
                    UserId = moderatorUserId,
                    GuestId = moderatorGuestId,
                    DisplayName = moderatorDisplayName,
                    IsGuest = isGuest,
                    IsModerator = true,
                    IsSpectator = false,
                    JoinedAtUtc = now,
                }
            ],
        };

        db.Rooms.Add(room);
        await db.SaveChangesAsync();

        if (moderatorUserId is not null)
            await roomCache.InvalidateUserRoomsAsync(moderatorUserId);

        logger.LogInformation("Room created: {RoomId} by {ParticipantId}", room.Id, moderatorParticipantId);
        return room;
    }

    public async Task<Result<EstimationRoom, RoomError>> JoinRoomAsync(
        Guid roomId, string participantId, string? userId, string? guestId,
        string displayName, bool isGuest)
    {
        var roomResult = await GetRoomWithAll(roomId);
        if (roomResult.IsFailure) return roomResult.Error;

        var room = roomResult.Value;

        if (room.Status == RoomStatus.Archived)
            return RoomErrors.Archived(roomId);

        var existing = room.Participants.FirstOrDefault(p => p.ParticipantId == participantId);
        if (existing is not null)
            return await HandleExistingParticipantJoin(roomId, participantId, displayName, existing, room);

        db.Participants.Add(new EstimationParticipant
        {
            Id = Guid.NewGuid(),
            RoomId = roomId,
            ParticipantId = participantId,
            UserId = userId,
            GuestId = guestId,
            DisplayName = displayName,
            IsGuest = isGuest,
            IsModerator = false,
            IsSpectator = false,
            JoinedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        await TouchRoomAsync(roomId);

        if (userId is not null)
            await roomCache.InvalidateUserRoomsAsync(userId);

        await InvalidateAndBroadcastAsync(roomId);
        logger.LogInformation("Participant {ParticipantId} joined room {RoomId}", participantId, roomId);
        return await ReloadRoom(roomId);
    }

    public async Task<UnitResult<RoomError>> LeaveRoomAsync(Guid roomId, string participantId)
    {
        var participant = await db.Participants
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.RoomId == roomId && p.ParticipantId == participantId);

        if (participant is null) return UnitResult.Success<RoomError>();
        if (!participant.IsPresent) return UnitResult.Success<RoomError>();

        var room = await db.Rooms.AsNoTracking().FirstOrDefaultAsync(r => r.Id == roomId);
        if (room is not null)
        {
            await db.Votes
                .Where(v => v.RoomId == roomId && v.ParticipantId == participantId && v.RoundNumber == room.RoundNumber)
                .ExecuteDeleteAsync();
        }

        await db.Participants
            .Where(p => p.RoomId == roomId && p.ParticipantId == participantId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.IsPresent, false));

        await TouchRoomAsync(roomId);

        if (participant.UserId is not null)
            await roomCache.InvalidateUserRoomsAsync(participant.UserId);

        await InvalidateAndBroadcastAsync(roomId);
        logger.LogInformation("Participant {ParticipantId} left room {RoomId} (marked absent)", participantId, roomId);
        return UnitResult.Success<RoomError>();
    }

    public async Task<Result<EstimationRoom, RoomError>> ChangeModeAsync(
        Guid roomId, string participantId, bool isSpectator)
    {
        var roomResult = await GetRoomWithAll(roomId);
        if (roomResult.IsFailure) return roomResult.Error;

        var room = roomResult.Value;

        if (room.Status == RoomStatus.Archived)
            return RoomErrors.Archived(roomId);

        var participant = room.Participants.FirstOrDefault(p => p.ParticipantId == participantId);
        if (participant is null)
            return RoomErrors.ParticipantNotFound(participantId);

        await db.Participants
            .Where(p => p.RoomId == roomId && p.ParticipantId == participantId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.IsSpectator, isSpectator));

        if (isSpectator && room.Status == RoomStatus.Voting)
        {
            await db.Votes
                .Where(v => v.RoomId == roomId && v.ParticipantId == participantId && v.RoundNumber == room.RoundNumber)
                .ExecuteDeleteAsync();
        }

        await TouchRoomAsync(roomId);
        await InvalidateAndBroadcastAsync(roomId);
        logger.LogInformation("Participant {ParticipantId} mode → {Mode} in room {RoomId}",
            participantId, isSpectator ? "Spectator" : "Participant", roomId);
        return await ReloadRoom(roomId);
    }

    /// <summary>Handles the case where a participant is re-joining (already exists in room).</summary>
    private async Task<Result<EstimationRoom, RoomError>> HandleExistingParticipantJoin(
        Guid roomId, string participantId, string displayName,
        EstimationParticipant existing, EstimationRoom room)
    {
        var nameChanged = !string.IsNullOrWhiteSpace(displayName) && existing.DisplayName != displayName;
        var wasAbsent = !existing.IsPresent;

        if (nameChanged)
        {
            var affectedRoomIds = await UpdateDisplayNameAcrossRoomsAsync(participantId, displayName);
            foreach (var affectedId in affectedRoomIds)
                await InvalidateAndBroadcastAsync(affectedId);
        }

        if (wasAbsent)
        {
            await db.Participants
                .Where(p => p.RoomId == roomId && p.ParticipantId == participantId)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.IsPresent, true));
            await TouchRoomAsync(roomId);
            await InvalidateAndBroadcastAsync(roomId);
            logger.LogInformation("Participant {ParticipantId} returned to room {RoomId}", participantId, roomId);
            return await ReloadRoom(roomId);
        }

        if (nameChanged)
            return await ReloadRoom(roomId);

        logger.LogDebug("Participant {ParticipantId} already in room {RoomId}, skipping join", participantId, roomId);
        return room;
    }
}