namespace Overflow.Common;

public static class CacheKeys
{
    public static string QuestionList(string sort, string? tag, int page, int pageSize)
        => $"questions:{sort}:{tag ?? "all"}:{page}:{pageSize}";

    public static string QuestionDetail(string questionId)
        => $"question:{questionId}";

    public static string TagList(string sort)
        => $"tags:list:{sort}";

    public const string TagValidation = "tags:all";

    public const string TrendingTags = "stats:trending-tags";
    public const string TopUsers = "stats:top-users";
}

public static class CacheTags
{
    public const string QuestionList = "question-list";
    public const string TagList = "tag-list";
    public const string TrendingTags = "trending-tags";
    public const string TopUsers = "top-users";
}