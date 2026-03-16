using System.ComponentModel.DataAnnotations;

namespace Overflow.EstimationService.Options;

public class RoomCleanupOptions
{
    public const string SectionName = "RoomCleanup";

    /// <summary>
    /// Number of days after archival before a room is permanently deleted.
    /// </summary>
    [Range(1, 365)]
    public int RetentionDays { get; set; }

    /// <summary>
    /// How often (in hours) the cleanup job runs.
    /// </summary>
    [Range(1, 72)]
    public int IntervalHours { get; set; }
}