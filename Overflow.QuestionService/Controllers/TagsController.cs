using CommandFlow;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Overflow.Common;
using Overflow.Common.CommonExtensions;
using Overflow.QuestionService.DTOs;
using Overflow.QuestionService.Features.Tags.Commands;
using Overflow.QuestionService.Features.Tags.Queries;
using Overflow.QuestionService.Models;

namespace Overflow.QuestionService.Controllers;

[ApiController]
[Route("[controller]")]
public class TagsController(ISender sender, ILogger<TagsController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<Tag>>> GetTags(string? sort)
    {
        var tags = await sender.Send(new GetTagsQuery(sort));
        return Ok(tags);
    }

    [Authorize(Roles = "admin")]
    [HttpGet("search")]
    public async Task<ActionResult<PaginationResult<Tag>>> SearchTags(
        [FromQuery] string? search, [FromQuery] int? page, [FromQuery] int? pageSize)
    {
        var result = await sender.Send(new SearchTagsQuery(search, page, pageSize));
        return Ok(result);
    }

    [Authorize(Roles = "admin")]
    [HttpPost]
    public async Task<ActionResult<Tag>> CreateTag(CreateTagDto dto)
    {
        var adminId = User.GetRequiredUserId();
        logger.LogInformation("Admin {AdminId} creating tag: Name={Name}, Slug={Slug}", adminId, dto.Name, dto.Slug);

        var result = await sender.Send(new CreateTagCommand(dto.Slug, dto.Name, dto.Description));
        if (!result.IsSuccess)
        {
            logger.LogWarning("Admin {AdminId} create tag failed: {Error}", adminId, result.Error);
            return Conflict(result.Error);
        }

        logger.LogInformation("Admin {AdminId} created tag {TagId}: {Name}", adminId, result.Value.Id, result.Value.Name);
        return CreatedAtAction(nameof(GetTag), new { id = result.Value.Id }, result.Value);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Tag>> GetTag(string id)
    {
        var tag = await sender.Send(new GetTagByIdQuery(id));
        return tag is null ? NotFound() : Ok(tag);
    }

    [Authorize(Roles = "admin")]
    [HttpPut("{id}")]
    public async Task<ActionResult<Tag>> UpdateTag(string id, UpdateTagDto dto)
    {
        var adminId = User.GetRequiredUserId();
        logger.LogInformation("Admin {AdminId} updating tag {TagId}: Name={Name}", adminId, id, dto.Name);

        var result = await sender.Send(new UpdateTagCommand(id, dto.Name, dto.Description));
        if (!result.IsSuccess)
        {
            logger.LogWarning("Admin {AdminId} update tag {TagId} failed: not found", adminId, id);
            return NotFound();
        }

        logger.LogInformation("Admin {AdminId} successfully updated tag {TagId}", adminId, id);
        return Ok(result.Value);
    }

    [Authorize(Roles = "admin")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteTag(string id)
    {
        var adminId = User.GetRequiredUserId();
        logger.LogInformation("Admin {AdminId} deleting tag {TagId}", adminId, id);

        var result = await sender.Send(new DeleteTagCommand(id));
        if (!result.IsSuccess)
        {
            logger.LogWarning("Admin {AdminId} delete tag {TagId} failed: not found", adminId, id);
            return NotFound();
        }

        logger.LogInformation("Admin {AdminId} successfully deleted tag {TagId}", adminId, id);
        return NoContent();
    }
}