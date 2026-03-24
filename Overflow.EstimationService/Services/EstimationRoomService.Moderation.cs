using System.Text.Json;
using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Overflow.EstimationService.Exceptions;
using Overflow.EstimationService.Models;

namespace Overflow.EstimationService.Services;

public partial class EstimationRoomService
{
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
            .Where(p => !p.IsSpectator && p.IsPresent)
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

        var taskName = TaskListHelper.GetTaskName(room.TasksJson, room.RoundNumber);

        db.RoundHistory.Add(new EstimationRoundHistory
        {
            Id = Guid.NewGuid(),
            RoomId = roomId,
            RoundNumber = room.RoundNumber,
            TaskName = taskName,
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

        await BroadcastRoomUpdateAsync(roomId);
        logger.LogInformation("Votes revealed in room {RoomId} by {ModeratorId}", roomId, moderatorId);
        return await ReloadRoom(roomId);
    }

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

        var newRound = DetermineNextRound(room);

        var now = DateTime.UtcNow;
        await db.Rooms.Where(r => r.Id == roomId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.RoundNumber, newRound)
                .SetProperty(r => r.Status, RoomStatus.Voting)
                .SetProperty(r => r.UpdatedAtUtc, now));

        await BroadcastRoomUpdateAsync(roomId);
        logger.LogInformation("Round reset in room {RoomId}, new round {Round}", roomId, newRound);
        return await ReloadRoom(roomId);
    }

    public async Task<Result<EstimationRoom, RoomError>> RevoteAsync(Guid roomId, string moderatorId,
        int? targetRound = null)
    {
        var roomResult = await GetRoomWithAll(roomId);
        if (roomResult.IsFailure) return roomResult.Error;

        var room = roomResult.Value;

        var moderatorCheck = EnsureModerator(room, moderatorId);
        if (moderatorCheck.IsFailure) return moderatorCheck.Error;

        if (room.Status == RoomStatus.Archived)
            return RoomErrors.Archived(roomId);

        var round = targetRound ?? room.RoundNumber;

        if (targetRound is null && room.Status != RoomStatus.Revealed)
            return RoomErrors.InvalidState("Can only revote after votes are revealed");

        if (round < 1)
            return RoomErrors.InvalidState($"Invalid round number: {round}");

        var hasHistoryEntry = room.RoundHistory.Any(h => h.RoundNumber == round);
        if (round != room.RoundNumber && !hasHistoryEntry)
            return RoomErrors.InvalidState($"Invalid round number: {round}");

        await db.Votes.Where(v => v.RoomId == roomId && v.RoundNumber == round).ExecuteDeleteAsync();
        await db.RoundHistory.Where(h => h.RoomId == roomId && h.RoundNumber == round).ExecuteDeleteAsync();

        var now = DateTime.UtcNow;
        await db.Rooms.Where(r => r.Id == roomId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.RoundNumber, round)
                .SetProperty(r => r.Status, RoomStatus.Voting)
                .SetProperty(r => r.UpdatedAtUtc, now));

        await BroadcastRoomUpdateAsync(roomId);
        logger.LogInformation("Revote started in room {RoomId} for round {Round} by {ModeratorId}",
            roomId, round, moderatorId);
        return await ReloadRoom(roomId);
    }

    public async Task<Result<EstimationRoom, RoomError>> RenameRoomAsync(Guid roomId, string moderatorId,
        string newTitle)
    {
        var roomResult = await GetRoomWithAll(roomId);
        if (roomResult.IsFailure) return roomResult.Error;

        var room = roomResult.Value;

        var moderatorCheck = EnsureModerator(room, moderatorId);
        if (moderatorCheck.IsFailure) return moderatorCheck.Error;

        if (room.Status == RoomStatus.Archived)
            return RoomErrors.Archived(roomId);

        var trimmed = newTitle.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return RoomErrors.InvalidState("Room title cannot be empty");

        if (trimmed.Length > MaxTitleLength)
            return RoomErrors.InvalidState($"Room title must be {MaxTitleLength} characters or fewer");

        var now = DateTime.UtcNow;
        await db.Rooms.Where(r => r.Id == roomId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Title, trimmed)
                .SetProperty(r => r.UpdatedAtUtc, now));

        await BroadcastRoomUpdateAsync(roomId);
        logger.LogInformation("Room {RoomId} renamed to '{Title}' by {ModeratorId}", roomId, trimmed, moderatorId);
        return await ReloadRoom(roomId);
    }

    public async Task<Result<EstimationRoom, RoomError>> UpdateTasksAsync(Guid roomId, string moderatorId,
        List<string> tasks)
    {
        var roomResult = await GetRoomWithAll(roomId);
        if (roomResult.IsFailure) return roomResult.Error;

        var room = roomResult.Value;

        var moderatorCheck = EnsureModerator(room, moderatorId);
        if (moderatorCheck.IsFailure) return moderatorCheck.Error;

        if (room.Status == RoomStatus.Archived)
            return RoomErrors.Archived(roomId);

        var tasksJson = TaskListHelper.SerializeTasks(tasks);

        var now = DateTime.UtcNow;
        await db.Rooms.Where(r => r.Id == roomId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.TasksJson, tasksJson)
                .SetProperty(r => r.UpdatedAtUtc, now));

        await BroadcastRoomUpdateAsync(roomId);
        logger.LogInformation("Tasks updated in room {RoomId} by {ModeratorId}", roomId, moderatorId);
        return await ReloadRoom(roomId);
    }

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

        await BroadcastRoomUpdateAsync(roomId);
        logger.LogInformation("Room {RoomId} archived by {ModeratorId}", roomId, moderatorId);
        return await ReloadRoom(roomId);
    }

    public async Task<UnitResult<RoomError>> DeleteRoomAsync(Guid roomId, string moderatorId)
    {
        var roomResult = await GetRoomWithAll(roomId);
        if (roomResult.IsFailure) return roomResult.Error;

        var room = roomResult.Value;

        var moderatorCheck = EnsureModerator(room, moderatorId);
        if (moderatorCheck.IsFailure) return moderatorCheck.Error;

        await db.Rooms.Where(r => r.Id == roomId).ExecuteDeleteAsync();

        await BroadcastRoomUpdateAsync(roomId);
        logger.LogInformation("Room {RoomId} deleted by {ModeratorId}", roomId, moderatorId);
        return UnitResult.Success<RoomError>();
    }

    /// <summary>
    /// Determines the next round number after a reset, skipping already-completed rounds
    /// when a task list is present.
    /// </summary>
    private static int DetermineNextRound(EstimationRoom room)
    {
        var completedRounds = room.RoundHistory.Select(h => h.RoundNumber).ToHashSet();
        completedRounds.Add(room.RoundNumber);

        var totalTasks = TaskListHelper.ParseTasks(room.TasksJson)?.Count ?? 0;

        if (totalTasks <= 0)
            return room.RoundNumber + 1;

        var nextUnestimated = Enumerable.Range(1, totalTasks)
            .FirstOrDefault(r => !completedRounds.Contains(r));

        return nextUnestimated != 0 ? nextUnestimated : totalTasks + 1;
    }
}