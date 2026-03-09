namespace Overflow.EstimationService.Events;

public record RoomCreated(
    Guid RoomId,
    string Code,
    string Title,
    string ModeratorUserId,
    string ModeratorDisplayName,
    string DeckType,
    string[] DeckValues,
    DateTime CreatedAtUtc
);

public record ParticipantJoined(
    string ParticipantId,
    string? UserId,
    string? GuestId,
    string DisplayName,
    bool IsGuest,
    DateTime JoinedAtUtc
);

public record ParticipantModeChanged(
    string ParticipantId,
    bool IsSpectator,
    DateTime ChangedAtUtc
);

public record ParticipantLeft(
    string ParticipantId,
    DateTime LeftAtUtc
);

public record VoteSubmitted(
    string ParticipantId,
    int RoundNumber,
    string Value,
    DateTime SubmittedAtUtc
);

public record VoteCleared(
    string ParticipantId,
    int RoundNumber,
    DateTime ClearedAtUtc
);

public record VotesRevealed(
    int RoundNumber,
    DateTime RevealedAtUtc
);

public record RoundReset(
    int NewRoundNumber,
    DateTime ResetAtUtc
);

public record RoomArchived(
    DateTime ArchivedAtUtc
);