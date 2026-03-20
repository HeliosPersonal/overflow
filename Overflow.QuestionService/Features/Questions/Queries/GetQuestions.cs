using CommandFlow;
using Microsoft.EntityFrameworkCore;
using Overflow.Common;
using Overflow.QuestionService.Data;
using Overflow.QuestionService.Models;
using Overflow.QuestionService.RequestHelpers;
using ZiggyCreatures.Caching.Fusion;

namespace Overflow.QuestionService.Features.Questions.Queries;

public record GetQuestionsQuery(QuestionsQuery Params) : IQuery<PaginationResult<Question>>;

public class GetQuestionsHandler(
    QuestionDbContext db,
    IFusionCache cache) : IRequestHandler<GetQuestionsQuery, PaginationResult<Question>>
{
    public async Task<PaginationResult<Question>> Handle(GetQuestionsQuery request, CancellationToken ct)
    {
        var q = request.Params;
        var cacheKey = CacheKeys.QuestionList(q.Sort ?? "newest", q.Tag, q.SafePage, q.CappedPageSize);

        return await cache.GetOrSetAsync(cacheKey, async _ =>
        {
            var query = db.Questions.AsNoTracking().AsQueryable();

            if (!string.IsNullOrEmpty(q.Tag))
                query = query.Where(x => x.TagSlugs.Contains(q.Tag));

            query = q.Sort switch
            {
                "newest" => query.OrderByDescending(x => x.CreatedAt),
                "active" => query.OrderByDescending(x => new[]
                {
                    x.CreatedAt,
                    x.UpdatedAt ?? DateTime.MinValue,
                    x.Answers.Max(a => (DateTime?)a.CreatedAt) ?? DateTime.MinValue,
                    x.Answers.Max(a => a.UpdatedAt) ?? DateTime.MinValue,
                }.Max()),
                "unanswered" => query.Where(x => x.AnswerCount == 0).OrderByDescending(x => x.CreatedAt),
                _ => query.OrderByDescending(x => x.CreatedAt)
            };

            return await query.ToPaginatedListAsync(q);
        }, tags: [CacheTags.QuestionList]);
    }
}