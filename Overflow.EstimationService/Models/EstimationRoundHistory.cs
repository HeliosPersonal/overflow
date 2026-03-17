using System.Text.Json.Serialization;

namespace Overflow.EstimationService.Models;

public class EstimationRoundHistory
{
    public Guid Id { get; set; }
    public Guid RoomId { get; set; }
    public int RoundNumber { get; set; }

    /// <summary>
    /// The task name that was estimated in this round (null when room has no task list).
    /// </summary>
    public string? TaskName { get; set; }

    public int VoterCount { get; set; }
    public string DistributionJson { get; set; } = "{}"; // JSON dict: { "5": 3, "8": 2 }
    public double? NumericAverage { get; set; }
    public string? NumericAverageDisplay { get; set; }
    public DateTime RevealedAtUtc { get; set; }

    [JsonIgnore] public EstimationRoom Room { get; set; } = null!;
}