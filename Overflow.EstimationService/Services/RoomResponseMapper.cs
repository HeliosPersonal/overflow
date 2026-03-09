using Overflow.EstimationService.DTOs;
using Overflow.EstimationService.Models;

namespace Overflow.EstimationService.Services;

/// <summary>
/// Maps the raw <see cref="EstimationRoomView"/> to a viewer-scoped <see cref="RoomResponse"/>.
/// Handles vote visibility rules: before reveal only the viewer sees their own vote;
/// after reveal all votes are visible.
/// </summary>
public static class RoomResponseMapper
{
    public static RoomResponse ToResponse(EstimationRoomView room, string viewerParticipantId, string baseUrl)
    {
        var isRevealed = room.Status == RoomStatus.Revealed;
        var isArchived = room.Status == RoomStatus.Archived;
        var currentRoundVotes = room.Votes
            .Where(v => v.RoundNumber == room.RoundNumber)
            .ToList();

        var viewer = room.Participants.FirstOrDefault(p => p.ParticipantId == viewerParticipantId);

        var viewerVote = currentRoundVotes
            .FirstOrDefault(v => v.ParticipantId == viewerParticipantId)?.Value;

        var activeParticipants = room.Participants
            .Where(p => p.LeftAtUtc is null && !p.IsSpectator)
            .ToList();

        var spectators = room.Participants
            .Where(p => p.LeftAtUtc is null && p.IsSpectator)
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

            return new ParticipantResponse(
                p.ParticipantId,
                p.DisplayName,
                p.IsGuest,
                p.IsModerator,
                p.IsSpectator,
                hasVoted && !p.IsSpectator,
                revealedVote,
                p.LeftAtUtc is null
            );
        }).ToList();

        var deck = new DeckResponse(room.DeckType, Decks.GetOrDefault(room.DeckType).Name, room.DeckValues);

        return new RoomResponse(
            Code: room.Code,
            Title: room.Title,
            CanonicalUrl: $"{baseUrl.TrimEnd('/')}/planning-poker/{room.Code}",
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
                room.Status,
                isRevealed || isArchived,
                distribution,
                numericAverage,
                numericAverageDisplay,
                activeParticipants.Count,
                spectators.Count,
                deck
            )
        );
    }
}