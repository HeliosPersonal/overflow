namespace Overflow.EstimationService.DTOs;

// --- Request DTOs ---

public record CreateRoomRequest(string Title, string? DeckType = null);

public record JoinRoomRequest(string? DisplayName = null);

public record ChangeModeRequest(bool IsSpectator);

public record SubmitVoteRequest(string Value);

// --- Response DTOs ---

public record RoomResponse(
    string Code,
    string Title,
    string CanonicalUrl,
    string Status,
    int RoundNumber,
    DeckResponse Deck,
    bool IsArchived,
    bool IsReadOnly,
    ViewerResponse Viewer,
    List<ParticipantResponse> Participants,
    RoundSummaryResponse RoundSummary
);

public record DeckResponse(string Id, string Name, string[] Values);

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
    string Status,
    bool VotesRevealed,
    Dictionary<string, int>? Distribution,
    double? NumericAverage,
    string? NumericAverageDisplay,
    int ActiveVoterCount,
    int SpectatorCount,
    DeckResponse AvailableDeck
);

public record DeckDefinitionResponse(string Id, string Name, string[] Values);