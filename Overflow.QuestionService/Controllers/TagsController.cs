using CommandFlow;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Overflow.QuestionService.DTOs;
using Overflow.QuestionService.Features.Tags.Commands;
using Overflow.QuestionService.Features.Tags.Queries;
using Overflow.QuestionService.Models;

namespace Overflow.QuestionService.Controllers;

[ApiController]
[Route("[controller]")]
public class TagsController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<Tag>>> GetTags(string? sort)
    {
        var tags = await sender.Send(new GetTagsQuery(sort));
        return Ok(tags);
    }

    [Authorize(Roles = "admin")]
    [HttpPost]
    public async Task<ActionResult<Tag>> CreateTag(CreateTagDto dto)
    {
        var result = await sender.Send(new CreateTagCommand(dto.Slug, dto.Name, dto.Description));
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetTag), new { id = result.Value.Id }, result.Value)
            : Conflict(result.Error);
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
        var result = await sender.Send(new UpdateTagCommand(id, dto.Name, dto.Description));
        return result.IsSuccess ? Ok(result.Value) : NotFound();
    }

    [Authorize(Roles = "admin")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteTag(string id)
    {
        var result = await sender.Send(new DeleteTagCommand(id));
        return result.IsSuccess ? NoContent() : NotFound();
    }
}