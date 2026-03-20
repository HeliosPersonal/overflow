using System.Text.RegularExpressions;
using CommandFlow;
using Microsoft.Extensions.Options;
using Overflow.SearchService.Models;
using Overflow.SearchService.Options;
using Typesense;

namespace Overflow.SearchService.Features.Search.Queries;

// ─── Full-text Search ───────────────────────────────────────────────────

public record SearchQuestionsQuery(string Query) : IQuery<IEnumerable<SearchQuestion>>;

public partial class SearchQuestionsHandler(
    ITypesenseClient client,
    IOptions<TypesenseOptions> options,
    ILogger<SearchQuestionsHandler> logger) : IRequestHandler<SearchQuestionsQuery, IEnumerable<SearchQuestion>>
{
    [GeneratedRegex(@"\[(.*?)\]")]
    private static partial Regex TagFilterRegex();

    public async Task<IEnumerable<SearchQuestion>> Handle(SearchQuestionsQuery request,
        CancellationToken cancellationToken)
    {
        var query = request.Query;
        string? tag = null;
        var tagMatch = TagFilterRegex().Match(query);
        if (tagMatch.Success)
        {
            tag = tagMatch.Groups[1].Value;
            query = query.Replace(tagMatch.Value, "").Trim();
        }

        var searchParams = new SearchParameters(query, "title,content");
        if (!string.IsNullOrWhiteSpace(tag))
            searchParams.FilterBy = $"tags:=[{tag}]";

        var result = await client.Search<SearchQuestion>(options.Value.CollectionName, searchParams);
        logger.LogInformation("Search completed: query='{Query}', tag='{Tag}', found={Count}",
            query, tag, result.Found);
        return result.Hits.Select(hit => hit.Document);
    }
}

// ─── Similar Titles Search ──────────────────────────────────────────────

public record SearchSimilarTitlesQuery(string Query) : IQuery<IEnumerable<SearchQuestion>>;

public class SearchSimilarTitlesHandler(
    ITypesenseClient client,
    IOptions<TypesenseOptions> options) : IRequestHandler<SearchSimilarTitlesQuery, IEnumerable<SearchQuestion>>
{
    public async Task<IEnumerable<SearchQuestion>> Handle(SearchSimilarTitlesQuery request,
        CancellationToken cancellationToken)
    {
        var searchParams = new SearchParameters(request.Query, "title");
        var result = await client.Search<SearchQuestion>(options.Value.CollectionName, searchParams);
        return result.Hits.Select(hit => hit.Document);
    }
}