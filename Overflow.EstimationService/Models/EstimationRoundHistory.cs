namespace Overflow.EstimationService.Models;

public class EstimationRoundHistory
{
    public Guid Id { get; set; }
    public Guid RoomId { get; set; }
    public int RoundNumber { get; set; }
    public int VoterCount { get; set; }
    public string DistributionJson { get; set; } = "{}"; // JSON dict: { "5": 3, "8": 2 }
    public double? NumericAverage { get; set; }
    public string? NumericAverageDisplay { get; set; }
    public DateTime RevealedAtUtc { get; set; }

    public EstimationRoom Room { get; set; } = null!;
}