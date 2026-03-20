using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Overflow.SearchService.Models;
using Overflow.SearchService.Options;
using Typesense;

namespace Overflow.SearchService.Controllers;

[ApiController]
[Route("[controller]")]
public class SearchController(
    ITypesenseClient client,
    IOptions<TypesenseOptions> typesenseOptions,
    ILogger<SearchController> logger) : ControllerBase
{
    private readonly string _collectionName = typesenseOptions.Value.CollectionName;

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            logger.LogWarning("Search attempted with empty query");
            return BadRequest("Query parameter is required");
        }

        string? tag = null;
        var tagMatch = Regex.Match(query, @"\[(.*?)\]");
        if (tagMatch.Success)
        {
            tag = tagMatch.Groups[1].Value;
            query = query.Replace(tagMatch.Value, "").Trim();
            logger.LogDebug("Extracted tag filter: {Tag} from query", tag);
        }

        var searchParams = new SearchParameters(query, "title,content");
        if (!string.IsNullOrWhiteSpace(tag))
        {
            searchParams.FilterBy = $"tags:=[{tag}]";
        }

        try
        {
            var result = await client.Search<SearchQuestion>(_collectionName, searchParams);
            logger.LogInformation("Search completed: query='{Query}', tag='{Tag}', found={Count}",
                query, tag, result.Found);
            return Ok(result.Hits.Select(hit => hit.Document));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Typesense search failed for query: {Query}", query);
            return Problem("Search failed", ex.Message);
        }
    }

    [HttpGet("similar-titles")]
    public async Task<IActionResult> SearchSimilarTitles([FromQuery] string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            logger.LogWarning("Similar titles search attempted with empty query");
            return BadRequest("Query parameter is required");
        }

        var searchParams = new SearchParameters(query, "title");

        try
        {
            var result = await client.Search<SearchQuestion>(_collectionName, searchParams);
            logger.LogDebug("Similar titles search: query='{Query}', found={Count}", query, result.Found);
            return Ok(result.Hits.Select(hit => hit.Document));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Similar titles search failed for query: {Query}", query);
            return Problem("Search failed", ex.Message);
        }
    }
}