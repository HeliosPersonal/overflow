using Marten;
using Microsoft.AspNetCore.Mvc;
using Overflow.Common;
using Overflow.StatsService.Models;
using ZiggyCreatures.Caching.Fusion;

namespace Overflow.StatsService.Controllers;

[ApiController]
[Route("[controller]")]
public class StatsController(
    IQuerySession session,
    IFusionCache cache,
    ILogger<StatsController> logger) : ControllerBase
{
    [HttpGet("trending-tags")]
    public async Task<IActionResult> GetTrendingTags()
    {
        var result = await cache.GetOrSetAsync(CacheKeys.TrendingTags, async _ =>
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
            var start = today.AddDays(-6);

            var rows = await session.Query<TagDailyUsage>()
                .Where(x => x.Date >= start && x.Date <= today)
                .Select(x => new { x.Tag, x.Count })
                .ToListAsync();

            return rows
                .GroupBy(x => x.Tag)
                .Select(x => new { tag = x.Key, count = x.Sum(t => t.Count) })
                .OrderByDescending(x => x.count)
                .Take(5)
                .ToList();
        }, tags: [CacheTags.TrendingTags]);

        logger.LogDebug("Trending tags retrieved: {Count} tags over last 7 days", result.Count);
        return Ok(result);
    }

    [HttpGet("top-users")]
    public async Task<IActionResult> GetTopUsers()
    {
        var result = await cache.GetOrSetAsync(CacheKeys.TopUsers, async _ =>
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
            var start = today.AddDays(-6);

            var rows = await session.Query<UserDailyReputation>()
                .Where(x => x.Date >= start && x.Date <= today)
                .Select(x => new { x.UserId, x.Delta })
                .ToListAsync();

            return rows.GroupBy(x => x.UserId)
                .Select(g => new { userId = g.Key, delta = g.Sum(t => t.Delta) })
                .OrderByDescending(x => x.delta)
                .Take(5)
                .ToList();
        }, tags: [CacheTags.TopUsers]);

        logger.LogDebug("Top users retrieved: {Count} users by reputation delta over last 7 days", result.Count);
        return Ok(result);
    }
}