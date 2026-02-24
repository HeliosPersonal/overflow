namespace Overflow.SearchService.Controllers;

// [ApiController]
// [Route("[controller]")]
// public class SearchController(ITypesenseClient client, ILogger<SearchController> logger) : ControllerBase
// {
//     /// <summary>
//     /// Search questions by query with optional tag filtering.
//     /// Use [tag] syntax in query to filter by tag, e.g., "async [aspire]"
//     /// </summary>
//     /// <param name="query">Search query. Use [tag] to filter by tag.</param>
//     /// <param name="page">Page number (default: 1)</param>
//     /// <param name="perPage">Results per page (default: 10, max: 100)</param>
//     /// <returns>List of matching questions</returns>
//     [HttpGet]
//     public async Task<ActionResult<SearchResponse>> Search(
//         [FromQuery] string query,
//         [FromQuery] int page = 1,
//         [FromQuery] int perPage = 10)
//     {
//         if (string.IsNullOrWhiteSpace(query))
//         {
//             return BadRequest(new { error = "Query parameter is required" });
//         }
//
//         if (perPage > 100)
//         {
//             perPage = 100;
//         }
//
//         try
//         {
//             // Extract tag from query using [tag] syntax
//             string? tag = null;
//             var tagMatch = Regex.Match(query, @"\[(.*?)\]");
//             if (tagMatch.Success)
//             {
//                 tag = tagMatch.Groups[1].Value;
//                 query = query.Replace(tagMatch.Value, "").Trim();
//                 logger.LogDebug("Extracted tag filter: {Tag}", tag);
//             }
//
//             var searchParams = new SearchParameters(query, "title,content")
//             {
//                 Page = page,
//                 PerPage = perPage
//             };
//
//             if (!string.IsNullOrWhiteSpace(tag))
//             {
//                 searchParams.FilterBy = $"tags:=[{tag}]";
//             }
//
//             logger.LogInformation("Searching questions: query='{Query}', tag='{Tag}', page={Page}", query, tag, page);
//
//             var result = await client.Search<SearchQuestion>("questions", searchParams);
//
//             var response = new SearchResponse
//             {
//                 Results = result.Hits.Select(hit => hit.Document).ToList(),
//                 TotalFound = result.Found,
//                 Page = page,
//                 PerPage = perPage,
//                 Query = query,
//                 Tag = tag
//             };
//
//             logger.LogInformation("Search completed: found {Count} results", result.Found);
//
//             return Ok(response);
//         }
//         catch (Exception ex)
//         {
//             logger.LogError(ex, "Search failed for query: {Query}", query);
//             return StatusCode(500, new { error = "Search failed", message = ex.Message });
//         }
//     }
//
//     /// <summary>
//     /// Search questions by similar titles only.
//     /// Useful for finding duplicate questions or suggestions.
//     /// </summary>
//     /// <param name="query">Title to search for</param>
//     /// <param name="limit">Maximum number of results (default: 5, max: 20)</param>
//     /// <returns>List of questions with similar titles</returns>
//     [HttpGet("similar-titles")]
//     public async Task<ActionResult<SimilarTitlesResponse>> SearchSimilarTitles(
//         [FromQuery] string query,
//         [FromQuery] int limit = 5)
//     {
//         if (string.IsNullOrWhiteSpace(query))
//         {
//             return BadRequest(new { error = "Query parameter is required" });
//         }
//
//         if (limit > 20)
//         {
//             limit = 20;
//         }
//
//         try
//         {
//             var searchParams = new SearchParameters(query, "title")
//             {
//                 PerPage = limit
//             };
//
//             logger.LogInformation("Searching similar titles for: {Query}", query);
//
//             var result = await client.Search<SearchQuestion>("questions", searchParams);
//
//             var response = new SimilarTitlesResponse
//             {
//                 Results = result.Hits.Select(hit => new SimilarTitle
//                 {
//                     Id = hit.Document.Id,
//                     Title = hit.Document.Title,
//                     Tags = hit.Document.Tags.ToList(),
//                     CreatedAt = hit.Document.CreatedAt,
//                     Score = hit.TextMatch ?? 0
//                 }).ToList(),
//                 Query = query,
//                 TotalFound = result.Found
//             };
//
//             logger.LogInformation("Similar titles search completed: found {Count} results", result.Found);
//
//             return Ok(response);
//         }
//         catch (Exception ex)
//         {
//             logger.LogError(ex, "Similar titles search failed for query: {Query}", query);
//             return StatusCode(500, new { error = "Search failed", message = ex.Message });
//         }
//     }
//
//     /// <summary>
//     /// Search questions by tag only.
//     /// </summary>
//     /// <param name="tag">Tag to filter by</param>
//     /// <param name="page">Page number (default: 1)</param>
//     /// <param name="perPage">Results per page (default: 10, max: 100)</param>
//     /// <returns>List of questions with the specified tag</returns>
//     [HttpGet("by-tag/{tag}")]
//     public async Task<ActionResult<SearchResponse>> SearchByTag(
//         string tag,
//         [FromQuery] int page = 1,
//         [FromQuery] int perPage = 10)
//     {
//         if (string.IsNullOrWhiteSpace(tag))
//         {
//             return BadRequest(new { error = "Tag parameter is required" });
//         }
//
//         if (perPage > 100)
//         {
//             perPage = 100;
//         }
//
//         try
//         {
//             var searchParams = new SearchParameters("*", "title,content")
//             {
//                 FilterBy = $"tags:=[{tag}]",
//                 Page = page,
//                 PerPage = perPage,
//                 SortBy = "createdAt:desc"
//             };
//
//             logger.LogInformation("Searching questions by tag: {Tag}, page={Page}", tag, page);
//
//             var result = await client.Search<SearchQuestion>("questions", searchParams);
//
//             var response = new SearchResponse
//             {
//                 Results = result.Hits.Select(hit => hit.Document).ToList(),
//                 TotalFound = result.Found,
//                 Page = page,
//                 PerPage = perPage,
//                 Query = "*",
//                 Tag = tag
//             };
//
//             logger.LogInformation("Tag search completed: found {Count} results", result.Found);
//
//             return Ok(response);
//         }
//         catch (Exception ex)
//         {
//             logger.LogError(ex, "Tag search failed for: {Tag}", tag);
//             return StatusCode(500, new { error = "Search failed", message = ex.Message });
//         }
//     }
// }

