using System.Text.Json;
using Overflow.EstimationService.DTOs;
using Overflow.EstimationService.Models;

namespace Overflow.EstimationService.Mapping;

/// <summary>
/// Maps the EF Core <see cref="EstimationRoom"/> entity to a viewer-scoped <see cref="RoomResponse"/>.
/// Handles vote visibility rules: before reveal only the viewer sees their own vote;
/// after reveal all votes are visible.
/// Avatar URLs are resolved from ProfileService at read time (passed in as a lookup dictionary)
/// rather than stored in the participant entity.
/// </summary>
public static class RoomResponseMapper
{
    public static RoomResponse ToResponse(
        EstimationRoom room,
        string viewerParticipantId,
        string baseUrl,
        IReadOnlyDictionary<string, string?>? avatarLookup = null)
    {
        avatarLookup ??= new Dictionary<string, string?>();

        var isRevealed = room.Status == RoomStatus.Revealed;
        var isArchived = room.Status == RoomStatus.Archived;
        var currentRoundVotes = room.Votes
            .Where(v => v.RoundNumber == room.RoundNumber)
            .ToList();

        var viewer = room.Participants.FirstOrDefault(p => p.ParticipantId == viewerParticipantId);

        var viewerVote = currentRoundVotes
            .FirstOrDefault(v => v.ParticipantId == viewerParticipantId)?.Value;

        var activeParticipants = room.Participants
            .Where(p => !p.IsSpectator && p.IsPresent)
            .ToList();

        var spectators = room.Participants
            .Where(p => p.IsSpectator && p.IsPresent)
            .ToList();

        // Build distribution + average only after reveal
        Dictionary<string, int>? distribution = null;
        double? numericAverage = null;
        string? numericAverageDisplay = null;

        if (isRevealed || isArchived)
        {
            var activeVoterIds = activeParticipants.Select(p => p.ParticipantId).ToHashSet();
            var roundVotesFromActiveVoters = currentRoundVotes
                .Where(v => activeVoterIds.Contains(v.ParticipantId))
                .ToList();

            distribution = roundVotesFromActiveVoters
                .GroupBy(v => v.Value)
                .ToDictionary(g => g.Key, g => g.Count());

            var numericValues = roundVotesFromActiveVoters
                .Select(v => double.TryParse(v.Value, out var n) ? (double?)n : null)
                .Where(n => n.HasValue)
                .Select(n => n!.Value)
                .ToList();

            if (numericValues.Count > 0 && numericValues.Count == roundVotesFromActiveVoters.Count)
            {
                numericAverage = numericValues.Average();
                numericAverageDisplay = Math.Round(numericAverage.Value, 1).ToString("F1");
            }
        }

        var participants = room.Participants.Select(p =>
        {
            var hasVoted = currentRoundVotes.Any(v => v.ParticipantId == p.ParticipantId);
            string? revealedVote = null;
            if (isRevealed || isArchived)
            {
                revealedVote = currentRoundVotes
                    .FirstOrDefault(v => v.ParticipantId == p.ParticipantId)?.Value;
            }

            var avatarUrl = p.UserId is not null ? avatarLookup.GetValueOrDefault(p.UserId) : null;

            return new ParticipantResponse(
                p.ParticipantId,
                p.DisplayName,
                avatarUrl,
                p.IsGuest,
                p.IsModerator,
                p.IsSpectator,
                hasVoted && !p.IsSpectator,
                revealedVote,
                p.IsPresent
            );
        }).ToList();

        var deck = new DeckResponse(room.DeckType, Decks.GetOrDefault(room.DeckType).Name,
            Decks.GetOrDefault(room.DeckType).Values);

        // Parse optional task list
        List<string>? tasks = null;
        if (!string.IsNullOrWhiteSpace(room.TasksJson))
        {
            try
            {
                tasks = JsonSerializer.Deserialize<List<string>>(room.TasksJson);
            }
            catch
            {
                /* ignore malformed JSON */
            }
        }

        string? currentTaskName = tasks is { Count: > 0 } && room.RoundNumber <= tasks.Count
            ? tasks[room.RoundNumber - 1]
            : null;

        var roundHistory = room.RoundHistory
            .OrderBy(h => h.RoundNumber)
            .Select(h => new RoundHistoryResponse(
                h.RoundNumber,
                h.TaskName,
                h.VoterCount,
                DeserializeDistribution(h.DistributionJson),
                h.NumericAverage,
                h.NumericAverageDisplay
            ))
            .ToList();

        return new RoomResponse(
            RoomId: room.Id,
            Title: room.Title,
            CanonicalUrl: $"{baseUrl.TrimEnd('/')}/planning-poker/{room.Id}",
            Status: room.Status,
            RoundNumber: room.RoundNumber,
            Deck: deck,
            IsArchived: isArchived,
            IsReadOnly: isArchived,
            Viewer: new ViewerResponse(
                viewer?.ParticipantId ?? viewerParticipantId,
                viewer?.UserId,
                viewer?.GuestId,
                viewer?.DisplayName ?? "Unknown",
                viewer?.IsGuest ?? false,
                viewer?.IsModerator ?? false,
                viewer?.IsSpectator ?? false,
                viewerVote
            ),
            Participants: participants,
            RoundSummary: new RoundSummaryResponse(
                room.RoundNumber,
                currentTaskName,
                room.Status,
                isRevealed || isArchived,
                distribution,
                numericAverage,
                numericAverageDisplay,
                activeParticipants.Count,
                spectators.Count,
                deck
            ),
            RoundHistory: roundHistory,
            Tasks: tasks,
            CurrentTaskName: currentTaskName
        );
    }

    private static Dictionary<string, int> DeserializeDistribution(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, int>>(json) ?? new();
        }
        catch
        {
            return new();
        }
    }
}