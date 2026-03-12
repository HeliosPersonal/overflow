using Overflow.EstimationService.Models;

namespace Overflow.EstimationService.DTOs;

public record RoomResponse(
    Guid RoomId,
    string Title,
    string CanonicalUrl,
    RoomStatus Status,
    int RoundNumber,
    DeckResponse Deck,
    bool IsArchived,
    bool IsReadOnly,
    ViewerResponse Viewer,
    List<ParticipantResponse> Participants,
    RoundSummaryResponse RoundSummary,
    List<RoundHistoryResponse> RoundHistory
);

public record DeckResponse(string Id, string Name, string[] Values);

public record DeckDefinitionResponse(string Id, string Name, string[] Values);

public record ViewerResponse(
    string ParticipantId,
    string? UserId,
    string? GuestId,
    string DisplayName,
    bool IsGuest,
    bool IsModerator,
    bool IsSpectator,
    string? SelectedVote
);

public record ParticipantResponse(
    string ParticipantId,
    string DisplayName,
    bool IsGuest,
    bool IsModerator,
    bool IsSpectator,
    bool HasVoted,
    string? RevealedVote,
    bool IsPresent
);

public record RoundSummaryResponse(
    int RoundNumber,
    RoomStatus Status,
    bool VotesRevealed,
    Dictionary<string, int>? Distribution,
    double? NumericAverage,
    string? NumericAverageDisplay,
    int ActiveVoterCount,
    int SpectatorCount,
    DeckResponse AvailableDeck
);

public record RoundHistoryResponse(
    int RoundNumber,
    int VoterCount,
    Dictionary<string, int> Distribution,
    double? NumericAverage,
    string? NumericAverageDisplay
);

public record RoomSummaryResponse(
    Guid RoomId,
    string Title,
    RoomStatus Status,
    int RoundNumber,
    int ParticipantCount,
    int CompletedRounds,
    DateTime CreatedAtUtc,
    DateTime? ArchivedAtUtc
);