using CommandFlow;
using Marten;
using Overflow.Common;
using Overflow.StatsService.Models;
using ZiggyCreatures.Caching.Fusion;

namespace Overflow.StatsService.Features.Stats.Queries;

// ─── Trending Tags ──────────────────────────────────────────────────────

public record GetTrendingTagsQuery : IQuery<object>;

public class GetTrendingTagsHandler(IQuerySession session, IFusionCache cache)
    : IRequestHandler<GetTrendingTagsQuery, object>
{
    public async Task<object> Handle(GetTrendingTagsQuery request, CancellationToken cancellationToken)
    {
        return (await cache.GetOrSetAsync(CacheKeys.TrendingTags, async _ =>
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
            var start = today.AddDays(-6);

            var rows = await session.Query<TagDailyUsage>()
                .Where(x => x.Date >= start && x.Date <= today)
                .Select(x => new { x.Tag, x.Count })
                .ToListAsync(cancellationToken);

            return rows
                .GroupBy(x => x.Tag)
                .Select(x => new { tag = x.Key, count = x.Sum(t => t.Count) })
                .OrderByDescending(x => x.count)
                .Take(5)
                .ToList();
        }, tags: [CacheTags.TrendingTags]))!;
    }
}

// ─── Top Users ──────────────────────────────────────────────────────────

public record GetTopUsersQuery : IQuery<object>;

public class GetTopUsersHandler(IQuerySession session, IFusionCache cache)
    : IRequestHandler<GetTopUsersQuery, object>
{
    public async Task<object> Handle(GetTopUsersQuery request, CancellationToken cancellationToken)
    {
        return (await cache.GetOrSetAsync(CacheKeys.TopUsers, async _ =>
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
            var start = today.AddDays(-6);

            var rows = await session.Query<UserDailyReputation>()
                .Where(x => x.Date >= start && x.Date <= today)
                .Select(x => new { x.UserId, x.Delta })
                .ToListAsync(cancellationToken);

            return rows.GroupBy(x => x.UserId)
                .Select(g => new { userId = g.Key, delta = g.Sum(t => t.Delta) })
                .OrderByDescending(x => x.delta)
                .Take(5)
                .ToList();
        }, tags: [CacheTags.TopUsers]))!;
    }
}