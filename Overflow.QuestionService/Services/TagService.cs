using Microsoft.EntityFrameworkCore;
using Overflow.Common;
using Overflow.QuestionService.Data;
using Overflow.QuestionService.Models;
using ZiggyCreatures.Caching.Fusion;

namespace Overflow.QuestionService.Services;

public class TagService(IFusionCache cache, QuestionDbContext db)
{
    private async Task<List<Tag>> GetTags()
    {
        return await cache.GetOrSetAsync(CacheKeys.TagValidation, async _ =>
        {
            var tags = await db.Tags.AsNoTracking().ToListAsync();
            return tags;
        }, new FusionCacheEntryOptions { Duration = TimeSpan.FromHours(2) });
    }

    public virtual async Task<bool> AreTagsValidAsync(List<string> slugs)
    {
        var tags = await GetTags();
        var tagSet = tags.Select(x => x.Slug).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return slugs.All(x => tagSet.Contains(x));
    }

    public virtual void InvalidateCache() => cache.Remove(CacheKeys.TagValidation);
}