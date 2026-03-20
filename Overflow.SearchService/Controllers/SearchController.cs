using CommandFlow;
using Microsoft.AspNetCore.Mvc;
using Overflow.SearchService.Features.Search.Queries;

namespace Overflow.SearchService.Controllers;

[ApiController]
[Route("[controller]")]
public class SearchController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return BadRequest("Query parameter is required");

        var results = await sender.Send(new SearchQuestionsQuery(query));
        return Ok(results);
    }

    [HttpGet("similar-titles")]
    public async Task<IActionResult> SearchSimilarTitles([FromQuery] string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return BadRequest("Query parameter is required");

        var results = await sender.Send(new SearchSimilarTitlesQuery(query));
        return Ok(results);
    }
}