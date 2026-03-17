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
    List<RoundHistoryResponse> RoundHistory,
    List<string>? Tasks,
    string? CurrentTaskName
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
    string? AvatarUrl,
    bool IsGuest,
    bool IsModerator,
    bool IsSpectator,
    bool HasVoted,
    string? RevealedVote,
    bool IsPresent
);

public record RoundSummaryResponse(
    int RoundNumber,
    string? TaskName,
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
    string? TaskName,
    int VoterCount,
    Dictionary<string, int> Distribution,
    double? NumericAverage,
    string? NumericAverageDisplay
);

public record ParticipantSummaryResponse(
    string DisplayName,
    string? AvatarUrl
);

public record RoomSummaryResponse(
    Guid RoomId,
    string Title,
    RoomStatus Status,
    int RoundNumber,
    int ParticipantCount,
    int CompletedRounds,
    DateTime CreatedAtUtc,
    DateTime? ArchivedAtUtc,
    bool IsModerator,
    int RetentionDays,
    string CreatorDisplayName,
    string? CreatorAvatarUrl,
    List<ParticipantSummaryResponse> Participants
);