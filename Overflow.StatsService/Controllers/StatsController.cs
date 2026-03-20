using CommandFlow;
using Microsoft.AspNetCore.Mvc;
using Overflow.StatsService.Features.Stats.Queries;

namespace Overflow.StatsService.Controllers;

[ApiController]
[Route("[controller]")]
public class StatsController(ISender sender) : ControllerBase
{
    [HttpGet("trending-tags")]
    public async Task<IActionResult> GetTrendingTags()
    {
        var result = await sender.Send(new GetTrendingTagsQuery());
        return Ok(result);
    }

    [HttpGet("top-users")]
    public async Task<IActionResult> GetTopUsers()
    {
        var result = await sender.Send(new GetTopUsersQuery());
        return Ok(result);
    }
}