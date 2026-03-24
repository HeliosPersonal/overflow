using System.ComponentModel.DataAnnotations;

namespace Overflow.EstimationService.Options;

public class RoomCleanupOptions
{
    public const string SectionName = "RoomCleanup";

    /// <summary>
    /// Days of inactivity (no <c>UpdatedAtUtc</c> change) before a non-archived room
    /// is automatically set to <c>Archived</c> status.
    /// </summary>
    [Range(1, 365)]
    public int InactiveDaysBeforeArchive { get; set; }

    /// <summary>
    /// Days after archival before an archived room is permanently deleted
    /// along with all participants, votes, and round history.
    /// </summary>
    [Range(1, 365)]
    public int ArchivedDaysBeforeDelete { get; set; }

    /// <summary>
    /// How often (in hours) the cleanup job runs.
    /// </summary>
    [Range(1, 72)]
    public int IntervalHours { get; set; }
}