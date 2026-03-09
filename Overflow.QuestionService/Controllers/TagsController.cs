using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Overflow.QuestionService.Data;
using Overflow.QuestionService.DTOs;
using Overflow.QuestionService.Models;
using Overflow.QuestionService.Services;

namespace Overflow.QuestionService.Controllers;

[ApiController]
[Route("[controller]")]
public class TagsController(QuestionDbContext db, TagService tagService, ILogger<TagsController> logger) : ControllerBase
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

    [Authorize(Roles = "admin")]
    [HttpPost]
    public async Task<ActionResult<Tag>> CreateTag(CreateTagDto dto)
    {
        var slug = dto.Slug.ToLowerInvariant().Trim();

        if (await db.Tags.AnyAsync(t => t.Slug == slug))
        {
            return Conflict($"A tag with slug '{slug}' already exists.");
        }

        var tag = new Tag
        {
            Id = slug,
            Name = dto.Name.Trim(),
            Slug = slug,
            Description = dto.Description.Trim()
        };

        db.Tags.Add(tag);
        await db.SaveChangesAsync();
        tagService.InvalidateCache();

        logger.LogInformation("Tag created: {Slug}", slug);
        return CreatedAtAction(nameof(GetTag), new { id = tag.Id }, tag);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Tag>> GetTag(string id)
    {
        var tag = await db.Tags.FindAsync(id);
        return tag is null ? NotFound() : Ok(tag);
    }

    [Authorize(Roles = "admin")]
    [HttpPut("{id}")]
    public async Task<ActionResult<Tag>> UpdateTag(string id, UpdateTagDto dto)
    {
        var tag = await db.Tags.FindAsync(id);
        if (tag is null)
        {
            return NotFound();
        }

        tag.Name = dto.Name.Trim();
        tag.Description = dto.Description.Trim();

        await db.SaveChangesAsync();
        tagService.InvalidateCache();

        logger.LogInformation("Tag updated: {Id}", id);
        return Ok(tag);
    }

    [Authorize(Roles = "admin")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteTag(string id)
    {
        var tag = await db.Tags.FindAsync(id);
        if (tag is null)
        {
            return NotFound();
        }

        db.Tags.Remove(tag);
        await db.SaveChangesAsync();
        tagService.InvalidateCache();

        logger.LogInformation("Tag deleted: {Id}", id);
        return NoContent();
    }
}