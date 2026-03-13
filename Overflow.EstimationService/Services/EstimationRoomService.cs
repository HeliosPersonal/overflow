using System.Text.Json;
using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Overflow.EstimationService.Data;
using Overflow.EstimationService.Exceptions;
using Overflow.EstimationService.Models;

namespace Overflow.EstimationService.Services;

/// <summary>
/// Manages estimation room state using EF Core + PostgreSQL.
/// All mutation methods return <c>Result&lt;EstimationRoom, RoomError&gt;</c> instead of throwing.
/// After each successful mutation, invalidates the FusionCache entry and publishes
/// a cross-pod broadcast so every pod pushes fresh state to its local WebSocket connections.
///
/// Reads use <see cref="RoomCacheService"/> (L1 in-memory + L2 Redis) to avoid hitting the DB
/// on every request. Mutations still go through EF Core directly, then invalidate the cache.
///
/// Reads use <c>AsNoTracking</c> to avoid stale-entity concurrency conflicts when
/// the WebSocket disconnect handler (separate DI scope) mutates the same room concurrently.
/// </summary>
public class EstimationRoomService(
    EstimationDbContext db,
    RoomCacheService roomCache,
    CrossPodBroadcastService crossPodBroadcast,
    ILogger<EstimationRoomService> logger)
{
    // ─── Create ──────────────────────────────────────────────────────────

    public async Task<Result<EstimationRoom, RoomError>> CreateRoomAsync(
        string title, string moderatorParticipantId, string? moderatorUserId,
        string? moderatorGuestId, string moderatorDisplayName, bool isGuest, string? deckType,
        string? avatarUrl = null)
    {
        var deck = Decks.GetOrDefault(deckType);
        var now = DateTime.UtcNow;

        var room = new EstimationRoom
        {
            Id = Guid.NewGuid(),
            Title = title.Trim(),
            ModeratorUserId = moderatorParticipantId,
            DeckType = deck.Id,
            Status = RoomStatus.Voting,
            RoundNumber = 1,
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
                    AvatarUrl = avatarUrl,
                    IsGuest = isGuest,
                    IsModerator = true,
                    IsSpectator = false,
                    JoinedAtUtc = now,
                }
            ],
        };

        db.Rooms.Add(room);
        await db.SaveChangesAsync();

        // Invalidate user rooms cache for the moderator
        if (moderatorUserId is not null)
            await roomCache.InvalidateUserRoomsAsync(moderatorUserId);

        logger.LogInformation("Room created: {RoomId} by {ParticipantId}", room.Id, moderatorParticipantId);
        return room;
    }

    // ─── Join ─────────────────────────────────────────────────────────────

    public async Task<Result<EstimationRoom, RoomError>> JoinRoomAsync(
        Guid roomId, string participantId, string? userId, string? guestId,
        string displayName, bool isGuest, string? avatarUrl = null)
    {
        var roomResult = await GetRoomWithAll(roomId);
        if (roomResult.IsFailure) return roomResult.Error;

        var room = roomResult.Value;

        if (room.Status == RoomStatus.Archived)
            return RoomErrors.Archived(roomId);

        var existing = room.Participants.FirstOrDefault(p => p.ParticipantId == participantId);
        if (existing is not null)
        {
            // Update display name and avatar across ALL rooms if changed (e.g. after profile edit or account upgrade).
            // Uses ExecuteUpdateAsync because entities are loaded with AsNoTracking.
            var nameChanged = !string.IsNullOrWhiteSpace(displayName) && existing.DisplayName != displayName;
            var avatarChanged = avatarUrl is not null && existing.AvatarUrl != avatarUrl;

            if (nameChanged || avatarChanged)
            {
                var affectedRoomIds = await UpdateProfileAcrossRoomsAsync(participantId,
                    nameChanged ? displayName : existing.DisplayName,
                    avatarChanged ? avatarUrl : existing.AvatarUrl);
                foreach (var affectedId in affectedRoomIds)
                    await InvalidateAndBroadcastAsync(affectedId);

                return await ReloadRoom(roomId);
            }

            logger.LogDebug("Participant {ParticipantId} already in room {RoomId}, skipping join",
                participantId, roomId);
            return room;
        }

        db.Participants.Add(new EstimationParticipant
        {
            Id = Guid.NewGuid(),
            RoomId = roomId,
            ParticipantId = participantId,
            UserId = userId,
            GuestId = guestId,
            DisplayName = displayName,
            AvatarUrl = avatarUrl,
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

    // ─── Mode change ──────────────────────────────────────────────────────

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

    // ─── Leave ────────────────────────────────────────────────────────────

    public async Task<UnitResult<RoomError>> LeaveRoomAsync(Guid roomId, string participantId)
    {
        var participant = await db.Participants
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.RoomId == roomId && p.ParticipantId == participantId);

        if (participant is null) return UnitResult.Success<RoomError>(); // Already gone

        await db.Votes
            .Where(v => v.RoomId == roomId && v.ParticipantId == participantId)
            .ExecuteDeleteAsync();

        await db.Participants
            .Where(p => p.RoomId == roomId && p.ParticipantId == participantId)
            .ExecuteDeleteAsync();

        await TouchRoomAsync(roomId);

        if (participant.UserId is not null)
            await roomCache.InvalidateUserRoomsAsync(participant.UserId);

        await InvalidateAndBroadcastAsync(roomId);
        logger.LogInformation("Participant {ParticipantId} left room {RoomId}", participantId, roomId);
        return UnitResult.Success<RoomError>();
    }

    // ─── Vote ─────────────────────────────────────────────────────────────

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

        await InvalidateAndBroadcastAsync(roomId);
        logger.LogDebug("Vote submitted by {ParticipantId} in room {RoomId}", participantId, roomId);
        return await ReloadRoom(roomId);
    }

    // ─── Clear vote ───────────────────────────────────────────────────────

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

        await InvalidateAndBroadcastAsync(roomId);
        logger.LogDebug("Vote cleared by {ParticipantId} in room {RoomId}", participantId, roomId);
        return UnitResult.Success<RoomError>();
    }

    // ─── Reveal ───────────────────────────────────────────────────────────

    public async Task<Result<EstimationRoom, RoomError>> RevealVotesAsync(Guid roomId, string moderatorId)
    {
        var roomResult = await GetRoomWithAll(roomId);
        if (roomResult.IsFailure) return roomResult.Error;

        var room = roomResult.Value;

        var moderatorCheck = EnsureModerator(room, moderatorId);
        if (moderatorCheck.IsFailure) return moderatorCheck.Error;

        if (room.Status != RoomStatus.Voting)
            return RoomErrors.InvalidState("Can only reveal during Voting status");

        var activeVoterIds = room.Participants
            .Where(p => !p.IsSpectator)
            .Select(p => p.ParticipantId)
            .ToHashSet();

        var roundVotes = room.Votes
            .Where(v => v.RoundNumber == room.RoundNumber && activeVoterIds.Contains(v.ParticipantId))
            .ToList();

        if (roundVotes.Count == 0)
            return RoomErrors.InvalidState("Cannot reveal — no votes have been cast yet");

        var distribution = roundVotes.GroupBy(v => v.Value).ToDictionary(g => g.Key, g => g.Count());

        double? numericAverage = null;
        string? numericAverageDisplay = null;
        var numericValues = roundVotes
            .Select(v => double.TryParse(v.Value, out var n) ? (double?)n : null)
            .Where(n => n.HasValue)
            .Select(n => n!.Value)
            .ToList();

        if (numericValues.Count > 0 && numericValues.Count == roundVotes.Count)
        {
            numericAverage = numericValues.Average();
            numericAverageDisplay = Math.Round(numericAverage.Value, 1).ToString("F1");
        }

        await db.RoundHistory
            .Where(h => h.RoomId == roomId && h.RoundNumber == room.RoundNumber)
            .ExecuteDeleteAsync();

        db.RoundHistory.Add(new EstimationRoundHistory
        {
            Id = Guid.NewGuid(),
            RoomId = roomId,
            RoundNumber = room.RoundNumber,
            VoterCount = roundVotes.Count,
            DistributionJson = JsonSerializer.Serialize(distribution),
            NumericAverage = numericAverage,
            NumericAverageDisplay = numericAverageDisplay,
            RevealedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var now = DateTime.UtcNow;
        await db.Rooms.Where(r => r.Id == roomId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Status, RoomStatus.Revealed)
                .SetProperty(r => r.UpdatedAtUtc, now));

        await InvalidateAndBroadcastAsync(roomId);
        logger.LogInformation("Votes revealed in room {RoomId} by {ModeratorId}", roomId, moderatorId);
        return await ReloadRoom(roomId);
    }

    // ─── Reset ────────────────────────────────────────────────────────────

    public async Task<Result<EstimationRoom, RoomError>> ResetRoundAsync(Guid roomId, string moderatorId)
    {
        var roomResult = await GetRoomWithAll(roomId);
        if (roomResult.IsFailure) return roomResult.Error;

        var room = roomResult.Value;

        var moderatorCheck = EnsureModerator(room, moderatorId);
        if (moderatorCheck.IsFailure) return moderatorCheck.Error;

        if (room.Status == RoomStatus.Archived)
            return RoomErrors.Archived(roomId);

        if (room.Status != RoomStatus.Revealed)
            return RoomErrors.InvalidState("Can only start a new round after votes are revealed");

        await db.Votes.Where(v => v.RoomId == roomId).ExecuteDeleteAsync();

        var newRound = room.RoundNumber + 1;
        var now = DateTime.UtcNow;
        await db.Rooms.Where(r => r.Id == roomId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.RoundNumber, newRound)
                .SetProperty(r => r.Status, RoomStatus.Voting)
                .SetProperty(r => r.UpdatedAtUtc, now));

        await InvalidateAndBroadcastAsync(roomId);
        logger.LogInformation("Round reset in room {RoomId}, new round {Round}", roomId, newRound);
        return await ReloadRoom(roomId);
    }

    // ─── Archive ──────────────────────────────────────────────────────────

    public async Task<Result<EstimationRoom, RoomError>> ArchiveRoomAsync(Guid roomId, string moderatorId)
    {
        var roomResult = await GetRoomWithAll(roomId);
        if (roomResult.IsFailure) return roomResult.Error;

        var room = roomResult.Value;

        var moderatorCheck = EnsureModerator(room, moderatorId);
        if (moderatorCheck.IsFailure) return moderatorCheck.Error;

        if (room.Status == RoomStatus.Archived)
            return RoomErrors.InvalidState("Room is already archived");

        var now = DateTime.UtcNow;
        await db.Rooms.Where(r => r.Id == roomId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Status, RoomStatus.Archived)
                .SetProperty(r => r.ArchivedAtUtc, now)
                .SetProperty(r => r.UpdatedAtUtc, now));

        // Invalidate user rooms cache for all participants
        var userIds = room.Participants.Select(p => p.UserId).Where(u => u is not null);
        await roomCache.InvalidateRoomAndUsersAsync(roomId, userIds);

        await crossPodBroadcast.PublishRoomUpdateAsync(roomId);
        logger.LogInformation("Room {RoomId} archived by {ModeratorId}", roomId, moderatorId);
        return await ReloadRoom(roomId);
    }

    // ─── Claim Guest ─────────────────────────────────────────────────────

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
            {
                var guestVotes = await db.Votes
                    .Where(v => v.RoomId == participant.RoomId && v.ParticipantId == guestId)
                    .ToListAsync();

                foreach (var vote in guestVotes)
                {
                    var hasExistingVote = await db.Votes
                        .AnyAsync(v => v.RoomId == participant.RoomId &&
                                       v.ParticipantId == userId && v.RoundNumber == vote.RoundNumber);
                    if (!hasExistingVote)
                        vote.ParticipantId = userId;
                    else
                        db.Votes.Remove(vote);
                }

                db.Participants.Remove(participant);
            }
            else
            {
                participant.ParticipantId = userId;
                participant.UserId = userId;
                participant.GuestId = null;
                participant.DisplayName = displayName;
                participant.IsGuest = false;

                var guestVotes = await db.Votes
                    .Where(v => v.RoomId == participant.RoomId && v.ParticipantId == guestId)
                    .ToListAsync();
                foreach (var vote in guestVotes)
                    vote.ParticipantId = userId;
            }

            affectedRoomIds.Add(participant.RoomId);
            claimed++;
        }

        await db.SaveChangesAsync();

        // Invalidate cache for all affected rooms
        foreach (var roomId in affectedRoomIds.Distinct())
            await roomCache.InvalidateRoomAsync(roomId);
        await roomCache.InvalidateUserRoomsAsync(userId);

        if (claimed > 0)
            logger.LogInformation("Claimed {Count} room(s) for guest {GuestId} → user {UserId}",
                claimed, guestId, userId);

        return claimed;
    }

    // ─── Queries ──────────────────────────────────────────────────────────

    public async Task<EstimationRoom?> GetRoomByIdAsync(Guid roomId)
    {
        return await roomCache.GetRoomAsync(roomId);
    }

    public async Task<IReadOnlyList<EstimationRoom>> GetRoomsForUserAsync(string userId)
    {
        return await roomCache.GetRoomsForUserAsync(userId);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────

    /// <summary>
    /// Invalidates the room cache and publishes a cross-pod broadcast notification.
    /// Every pod (including this one) will receive the notification, reload from DB (or L2 cache),
    /// and push fresh state to its local WebSocket connections.
    /// Failures are logged but never propagated — cache/broadcast issues must not break mutations.
    /// </summary>
    private async Task InvalidateAndBroadcastAsync(Guid roomId)
    {
        try
        {
            await roomCache.InvalidateRoomAsync(roomId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to invalidate cache for room {RoomId}", roomId);
        }

        try
        {
            await crossPodBroadcast.PublishRoomUpdateAsync(roomId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to publish cross-pod broadcast for room {RoomId}", roomId);
        }
    }

    /// <summary>
    /// Loads a room with all navigation properties as a read-only snapshot (AsNoTracking).
    /// For validation before mutations, bypasses cache to ensure freshest state.
    /// This prevents stale tracked entities from causing concurrency exceptions when
    /// the WebSocket disconnect handler mutates the same room from a different DI scope.
    /// </summary>
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

    /// <summary>
    /// Re-loads the room directly from DB after a mutation.
    /// Bypasses cache since we just invalidated it — ensures the caller gets the freshest state.
    /// </summary>
    private async Task<Result<EstimationRoom, RoomError>> ReloadRoom(Guid roomId)
    {
        return await GetRoomWithAll(roomId);
    }

    /// <summary>
    /// Updates only UpdatedAtUtc on the room without tracking the entity.
    /// </summary>
    private async Task TouchRoomAsync(Guid roomId)
    {
        var now = DateTime.UtcNow;
        await db.Rooms.Where(r => r.Id == roomId)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.UpdatedAtUtc, now));
    }

    /// <summary>
    /// Updates a participant's display name and avatar URL across ALL their rooms (not just the current one).
    /// Returns the list of affected room IDs so callers can broadcast WebSocket updates.
    /// Uses ExecuteUpdateAsync — works with AsNoTracking entities.
    /// </summary>
    private async Task<List<Guid>> UpdateProfileAcrossRoomsAsync(string participantId, string newDisplayName,
        string? newAvatarUrl)
    {
        var affectedRoomIds = await db.Participants
            .Where(p => p.ParticipantId == participantId
                        && (p.DisplayName != newDisplayName || p.AvatarUrl != newAvatarUrl))
            .Select(p => p.RoomId)
            .Distinct()
            .ToListAsync();

        if (affectedRoomIds.Count == 0) return affectedRoomIds;

        await db.Participants
            .Where(p => p.ParticipantId == participantId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.DisplayName, newDisplayName)
                .SetProperty(p => p.AvatarUrl, newAvatarUrl));

        logger.LogInformation(
            "Updated profile for participant {ParticipantId} across {Count} room(s)",
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