namespace Overflow.EstimationService.Models;

public class EstimationRoomView
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string ModeratorUserId { get; set; } = string.Empty;
    public string DeckType { get; set; } = "fibonacci";
    public string[] DeckValues { get; set; } = [];
    public string Status { get; set; } = RoomStatus.Voting;
    public int RoundNumber { get; set; } = 1;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? ArchivedAtUtc { get; set; }
    public List<ParticipantView> Participants { get; set; } = [];
    public List<RoundVoteView> Votes { get; set; } = [];
}

public class ParticipantView
{
    public string ParticipantId { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public string? GuestId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public bool IsGuest { get; set; }
    public bool IsModerator { get; set; }
    public bool IsSpectator { get; set; }
    public DateTime JoinedAtUtc { get; set; }
    public DateTime LastSeenAtUtc { get; set; }
    public DateTime? LeftAtUtc { get; set; }
}

public class RoundVoteView
{
    public int RoundNumber { get; set; }
    public string ParticipantId { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public DateTime SubmittedAtUtc { get; set; }
}

public static class RoomStatus
{
    public const string Voting = "Voting";
    public const string Revealed = "Revealed";
    public const string Archived = "Archived";
}