using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Overflow.QuestionService.Data;
using Overflow.QuestionService.Models;

namespace Overflow.QuestionService.Controllers;

[ApiController]
[Route("[controller]")]
public class TagsController(QuestionDbContext db, ILogger<TagsController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<Tag>>> GetTags(string? sort)
    {
        var query = db.Tags.AsQueryable();
        
        query = sort == "popular"
            ? query.OrderByDescending(x => x.UsageCount).ThenBy(x => x.Name)
            : query.OrderBy(x => x.Name);

        var tags = await query.ToListAsync();
        logger.LogDebug("Retrieved {Count} tags, sorted by {SortBy}", tags.Count, sort ?? "name");
        
        return tags;
    }
}