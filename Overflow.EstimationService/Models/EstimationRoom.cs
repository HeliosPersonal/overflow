using System.Text.Json.Serialization;

namespace Overflow.EstimationService.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RoomStatus
{
    Voting,
    Revealed,
    Archived
}

public class EstimationRoom
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ModeratorUserId { get; set; } = string.Empty;
    public string DeckType { get; set; } = "fibonacci";
    public RoomStatus Status { get; set; } = RoomStatus.Voting;
    public int RoundNumber { get; set; } = 1;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? ArchivedAtUtc { get; set; }

    // Navigation properties
    public List<EstimationParticipant> Participants { get; set; } = [];
    public List<EstimationVote> Votes { get; set; } = [];
    public List<EstimationRoundHistory> RoundHistory { get; set; } = [];
}