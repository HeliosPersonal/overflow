namespace Overflow.EstimationService.Options;

public class RoomCleanupOptions
{
    public const string SectionName = "RoomCleanup";

    /// <summary>
    /// Number of days after archival before a room is permanently deleted.
    /// </summary>
    public int RetentionDays { get; set; }

    /// <summary>
    /// How often (in hours) the cleanup job runs.
    /// </summary>
    public int IntervalHours { get; set; }
}