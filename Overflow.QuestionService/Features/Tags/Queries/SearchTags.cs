using CommandFlow;
using Microsoft.EntityFrameworkCore;
using Overflow.Common;
using Overflow.QuestionService.Data;
using Overflow.QuestionService.Models;

namespace Overflow.QuestionService.Features.Tags.Queries;

public record SearchTagsQuery(string? Search, int? Page, int? PageSize)
    : IQuery<PaginationResult<Tag>>;

public class SearchTagsHandler(QuestionDbContext db)
    : IRequestHandler<SearchTagsQuery, PaginationResult<Tag>>
{
    public async Task<PaginationResult<Tag>> Handle(SearchTagsQuery request, CancellationToken cancellationToken)
    {
        var query = db.Tags.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = $"%{request.Search.Trim()}%";
            query = query.Where(t =>
                EF.Functions.ILike(t.Name, search) ||
                EF.Functions.ILike(t.Slug, search));
        }

        query = query.OrderBy(t => t.Name);

        var paginationRequest = new PaginationRequest
        {
            Page = request.Page,
            PageSize = request.PageSize
        };

        return await query.ToPaginatedListAsync(paginationRequest);
    }
}

