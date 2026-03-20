using CommandFlow;
using Microsoft.EntityFrameworkCore;
using Overflow.Common;
using Overflow.QuestionService.Data;
using Overflow.QuestionService.Models;
using ZiggyCreatures.Caching.Fusion;

namespace Overflow.QuestionService.Features.Tags.Queries;

// ─── Get Tags (list) ────────────────────────────────────────────────────

public record GetTagsQuery(string? Sort) : IQuery<List<Tag>>;

public class GetTagsHandler(QuestionDbContext db, IFusionCache cache)
    : IRequestHandler<GetTagsQuery, List<Tag>>
{
    public async Task<List<Tag>> Handle(GetTagsQuery request, CancellationToken cancellationToken)
    {
        var cacheKey = CacheKeys.TagList(request.Sort ?? "name");

        return (await cache.GetOrSetAsync(cacheKey, async _ =>
        {
            var query = db.Tags.AsNoTracking().AsQueryable();

            query = request.Sort == "popular"
                ? query.OrderByDescending(x => x.UsageCount).ThenBy(x => x.Name)
                : query.OrderBy(x => x.Name);

            return await query.ToListAsync(cancellationToken);
        }, tags: [CacheTags.TagList]))!;
    }
}

// ─── Get Tag by ID ──────────────────────────────────────────────────────

public record GetTagByIdQuery(string Id) : IQuery<Tag?>;

public class GetTagByIdHandler(QuestionDbContext db) : IRequestHandler<GetTagByIdQuery, Tag?>
{
    public async Task<Tag?> Handle(GetTagByIdQuery request, CancellationToken cancellationToken)
    {
        return await db.Tags.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);
    }
}