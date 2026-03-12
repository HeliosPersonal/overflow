namespace Overflow.EstimationService.Models;

public class EstimationVote
{
    public Guid Id { get; set; }
    public Guid RoomId { get; set; }
    public int RoundNumber { get; set; }
    public string ParticipantId { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty; // must be in deck values
    public DateTime SubmittedAtUtc { get; set; }

    public EstimationRoom Room { get; set; } = null!;
}