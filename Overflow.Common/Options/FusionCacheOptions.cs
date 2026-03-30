using System.ComponentModel.DataAnnotations;

namespace Overflow.Common.Options;

public class FusionCacheOptions
{
    public const string SectionName = "FusionCache";

    /// <summary>
    /// The connection string name used to resolve the Redis connection string
    /// (e.g. "question-redis", "stat-redis", "estimation-redis").
    /// </summary>
    [Required]
    public string ConnectionStringName { get; set; } = null!;

    /// <summary>
    /// Default cache entry duration in seconds.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int DurationSeconds { get; set; }

    /// <summary>
    /// Whether fail-safe is enabled (serve stale data when the factory fails).
    /// </summary>
    public bool IsFailSafeEnabled { get; set; }

    /// <summary>
    /// Maximum duration (in seconds) to keep stale data when fail-safe is active.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int FailSafeMaxDurationSeconds { get; set; }

    /// <summary>
    /// Throttle duration (in seconds) between fail-safe retries.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int FailSafeThrottleDurationSeconds { get; set; }
}