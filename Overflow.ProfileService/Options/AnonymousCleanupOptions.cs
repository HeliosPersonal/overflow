using System.ComponentModel.DataAnnotations;

namespace Overflow.ProfileService.Options;

public class AnonymousCleanupOptions
{
    public const string SectionName = "AnonymousCleanup";

    /// <summary>
    /// Days since account creation before an anonymous (guest) user is deleted
    /// from Keycloak and ProfileService.
    /// </summary>
    [Range(1, 365)]
    public int GuestAccountMaxAgeDays { get; set; } = 30;

    /// <summary>
    /// How often (in hours) the cleanup job runs.
    /// </summary>
    [Range(1, 72)]
    public int IntervalHours { get; set; } = 24;
}