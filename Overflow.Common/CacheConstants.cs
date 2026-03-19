namespace Overflow.Common;

/// <summary>
/// Well-known cache keys used across services.
/// Eliminates magic strings and provides a single source of truth.
/// </summary>
public static class CacheKeys
{
    // ── QuestionService ──────────────────────────────────────────────────
    public static string QuestionList(string sort, string? tag, int page, int pageSize)
        => $"questions:{sort}:{tag ?? "all"}:{page}:{pageSize}";

    public static string QuestionDetail(string questionId)
        => $"question:{questionId}";

    public static string TagList(string sort)
        => $"tags:list:{sort}";

    public const string TagValidation = "tags:all";

    // ── StatsService ─────────────────────────────────────────────────────
    public const string TrendingTags = "stats:trending-tags";
    public const string TopUsers = "stats:top-users";
}

/// <summary>
/// Well-known cache tags used for bulk invalidation via FusionCache.
/// </summary>
public static class CacheTags
{
    public const string QuestionList = "question-list";
    public const string TagList = "tag-list";
    public const string TrendingTags = "trending-tags";
    public const string TopUsers = "top-users";
}