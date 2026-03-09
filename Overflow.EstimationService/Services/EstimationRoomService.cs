using Marten;
using Overflow.EstimationService.DTOs;
using Overflow.EstimationService.Events;
using Overflow.EstimationService.Models;

namespace Overflow.EstimationService.Services;

/// <summary>
/// Core service handling estimation room commands. All mutations append events
/// to a Marten event stream (one stream per room). The projected read model
/// <see cref="EstimationRoomView"/> is rebuilt automatically by the inline projection.
/// </summary>
public class EstimationRoomService(
    IDocumentSession session,
    ILogger<EstimationRoomService> logger)
{
    private const int MaxCodeRetries = 10;
    private const int CodeLength = 6;
    private static readonly char[] CodeChars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789".ToCharArray();

    // ─── Create ─────────────────────────────────────────────────────────

    public async Task<EstimationRoomView> CreateRoomAsync(
        string title, string moderatorUserId, string moderatorDisplayName, string? deckType)
    {
        var deck = Decks.GetOrDefault(deckType);
        var code = await GenerateUniqueCodeAsync();
        var roomId = Guid.NewGuid();

        var created = new RoomCreated(
            roomId, code, title.Trim(), moderatorUserId, moderatorDisplayName,
            deck.Id, deck.Values, DateTime.UtcNow);

        session.Events.StartStream<EstimationRoomView>(roomId, created);
        await session.SaveChangesAsync();

        logger.LogInformation("Room created: {Code} ({RoomId}) by {UserId}", code, roomId, moderatorUserId);

        return await GetRoomByIdAsync(roomId)
               ?? throw new InvalidOperationException("Room not found after creation");
    }

    // ─── Join ───────────────────────────────────────────────────────────

    public async Task<EstimationRoomView> JoinRoomAsync(
        string code, string participantId, string? userId, string? guestId,
        string displayName, bool isGuest)
    {
        var room = await GetRoomByCodeAsync(code)
                   ?? throw new RoomNotFoundException(code);

        if (room.Status == RoomStatus.Archived)
            throw new RoomArchivedException(code);

        var joined = new ParticipantJoined(
            participantId, userId, guestId, displayName, isGuest, DateTime.UtcNow);

        session.Events.Append(room.Id, joined);
        await session.SaveChangesAsync();

        logger.LogInformation("Participant {ParticipantId} joined room {Code}", participantId, code);

        return await GetRoomByIdAsync(room.Id)
               ?? throw new InvalidOperationException("Room not found after join");
    }

    // ─── Mode change ────────────────────────────────────────────────────

    public async Task<EstimationRoomView> ChangeModeAsync(string code, string participantId, bool isSpectator)
    {
        var room = await GetRoomByCodeAsync(code) ?? throw new RoomNotFoundException(code);
        if (room.Status == RoomStatus.Archived) throw new RoomArchivedException(code);

        var participant = room.Participants.FirstOrDefault(p => p.ParticipantId == participantId)
                          ?? throw new ParticipantNotFoundException(participantId);

        var modeChanged = new ParticipantModeChanged(participantId, isSpectator, DateTime.UtcNow);
        session.Events.Append(room.Id, modeChanged);

        // Clear vote if switching to spectator during a voting round (documented rule)
        if (isSpectator && room.Status == RoomStatus.Voting)
        {
            var hasVote = room.Votes.Any(v =>
                v.ParticipantId == participantId && v.RoundNumber == room.RoundNumber);
            if (hasVote)
            {
                session.Events.Append(room.Id,
                    new VoteCleared(participantId, room.RoundNumber, DateTime.UtcNow));
            }
        }

        await session.SaveChangesAsync();
        logger.LogInformation("Participant {ParticipantId} mode changed to {Mode} in room {Code}",
            participantId, isSpectator ? "Spectator" : "Participant", code);

        return await GetRoomByIdAsync(room.Id) ??
               throw new InvalidOperationException("Room not found after mode change");
    }

    // ─── Leave ──────────────────────────────────────────────────────────

    public async Task LeaveRoomAsync(string code, string participantId)
    {
        var room = await GetRoomByCodeAsync(code) ?? throw new RoomNotFoundException(code);

        session.Events.Append(room.Id, new ParticipantLeft(participantId, DateTime.UtcNow));
        await session.SaveChangesAsync();

        logger.LogInformation("Participant {ParticipantId} left room {Code}", participantId, code);
    }

    // ─── Vote ───────────────────────────────────────────────────────────

    public async Task<EstimationRoomView> SubmitVoteAsync(string code, string participantId, string value)
    {
        var room = await GetRoomByCodeAsync(code) ?? throw new RoomNotFoundException(code);
        if (room.Status != RoomStatus.Voting) throw new InvalidRoomStateException("Can only vote during Voting status");
        if (room.Status == RoomStatus.Archived) throw new RoomArchivedException(code);

        var participant = room.Participants.FirstOrDefault(p => p.ParticipantId == participantId)
                          ?? throw new ParticipantNotFoundException(participantId);
        if (participant.IsSpectator) throw new SpectatorCannotVoteException();
        if (participant.LeftAtUtc is not null) throw new InvalidOperationException("Cannot vote after leaving");

        if (!room.DeckValues.Contains(value))
            throw new InvalidVoteValueException(value);

        var vote = new VoteSubmitted(participantId, room.RoundNumber, value, DateTime.UtcNow);
        session.Events.Append(room.Id, vote);
        await session.SaveChangesAsync();

        logger.LogInformation("Vote submitted by {ParticipantId} in room {Code}", participantId, code);

        return await GetRoomByIdAsync(room.Id) ?? throw new InvalidOperationException("Room not found after vote");
    }

    // ─── Clear vote ─────────────────────────────────────────────────────

    public async Task<EstimationRoomView> ClearVoteAsync(string code, string participantId)
    {
        var room = await GetRoomByCodeAsync(code) ?? throw new RoomNotFoundException(code);
        if (room.Status != RoomStatus.Voting)
            throw new InvalidRoomStateException("Can only clear vote during Voting status");

        session.Events.Append(room.Id, new VoteCleared(participantId, room.RoundNumber, DateTime.UtcNow));
        await session.SaveChangesAsync();

        logger.LogInformation("Vote cleared by {ParticipantId} in room {Code}", participantId, code);

        return await GetRoomByIdAsync(room.Id) ?? throw new InvalidOperationException("Room not found after clear");
    }

    // ─── Reveal ─────────────────────────────────────────────────────────

    public async Task<EstimationRoomView> RevealVotesAsync(string code, string moderatorId)
    {
        var room = await GetRoomByCodeAsync(code) ?? throw new RoomNotFoundException(code);
        EnsureModerator(room, moderatorId);

        if (room.Status != RoomStatus.Voting)
            throw new InvalidRoomStateException("Can only reveal during Voting status");

        session.Events.Append(room.Id, new VotesRevealed(room.RoundNumber, DateTime.UtcNow));
        await session.SaveChangesAsync();

        logger.LogInformation("Votes revealed in room {Code} by moderator {ModeratorId}", code, moderatorId);

        return await GetRoomByIdAsync(room.Id) ?? throw new InvalidOperationException("Room not found after reveal");
    }

    // ─── Reset ──────────────────────────────────────────────────────────

    public async Task<EstimationRoomView> ResetRoundAsync(string code, string moderatorId)
    {
        var room = await GetRoomByCodeAsync(code) ?? throw new RoomNotFoundException(code);
        EnsureModerator(room, moderatorId);

        if (room.Status == RoomStatus.Archived)
            throw new RoomArchivedException(code);

        var newRound = room.RoundNumber + 1;
        session.Events.Append(room.Id, new RoundReset(newRound, DateTime.UtcNow));
        await session.SaveChangesAsync();

        logger.LogInformation("Round reset in room {Code}, new round {Round}", code, newRound);

        return await GetRoomByIdAsync(room.Id) ?? throw new InvalidOperationException("Room not found after reset");
    }

    // ─── Archive ────────────────────────────────────────────────────────

    public async Task<EstimationRoomView> ArchiveRoomAsync(string code, string moderatorId)
    {
        var room = await GetRoomByCodeAsync(code) ?? throw new RoomNotFoundException(code);
        EnsureModerator(room, moderatorId);

        if (room.Status == RoomStatus.Archived)
            throw new InvalidRoomStateException("Room is already archived");

        session.Events.Append(room.Id, new RoomArchived(DateTime.UtcNow));
        await session.SaveChangesAsync();

        logger.LogInformation("Room {Code} archived by moderator {ModeratorId}", code, moderatorId);

        return await GetRoomByIdAsync(room.Id) ?? throw new InvalidOperationException("Room not found after archive");
    }

    // ─── Queries ────────────────────────────────────────────────────────

    public async Task<EstimationRoomView?> GetRoomByCodeAsync(string code)
    {
        return await session.Query<EstimationRoomView>()
            .FirstOrDefaultAsync(r => r.Code == code);
    }

    public async Task<EstimationRoomView?> GetRoomByIdAsync(Guid id)
    {
        return await session.LoadAsync<EstimationRoomView>(id);
    }

    // ─── Helpers ────────────────────────────────────────────────────────

    private async Task<string> GenerateUniqueCodeAsync()
    {
        for (var i = 0; i < MaxCodeRetries; i++)
        {
            var code = GenerateCode();
            var exists = await session.Query<EstimationRoomView>().AnyAsync(r => r.Code == code);
            if (!exists) return code;
        }

        throw new InvalidOperationException($"Failed to generate unique room code after {MaxCodeRetries} attempts");
    }

    private static string GenerateCode()
    {
        return string.Create(CodeLength, CodeChars, (span, chars) =>
        {
            for (var i = 0; i < span.Length; i++)
                span[i] = chars[Random.Shared.Next(chars.Length)];
        });
    }

    private static void EnsureModerator(EstimationRoomView room, string userId)
    {
        if (room.ModeratorUserId != userId)
            throw new NotModeratorException();
    }
}

// ─── Domain exceptions ──────────────────────────────────────────────────────

public class RoomNotFoundException(string code) : Exception($"Room not found: {code}");

public class RoomArchivedException(string code) : Exception($"Room is archived: {code}");

public class ParticipantNotFoundException(string id) : Exception($"Participant not found: {id}");

public class NotModeratorException() : Exception("Only the moderator can perform this action");

public class SpectatorCannotVoteException() : Exception("Spectators cannot vote");

public class InvalidRoomStateException(string msg) : Exception(msg);

public class InvalidVoteValueException(string value) : Exception($"Invalid vote value: {value}");