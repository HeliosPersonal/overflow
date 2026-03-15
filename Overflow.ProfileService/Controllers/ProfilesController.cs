using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Overflow.ProfileService.Data;
using Overflow.ProfileService.DTOs;
using Overflow.ProfileService.Models;

namespace Overflow.ProfileService.Controllers;

[ApiController]
[Route("[controller]")]
public class ProfilesController(ProfileDbContext db, ILogger<ProfilesController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ProfileDto>>> GetProfiles(string? sortBy)
    {
        logger.LogDebug("Fetching profiles with sort: {SortBy}", sortBy ?? "displayName");

        var query = db.UserProfiles.AsQueryable();
        query = sortBy == "reputation"
            ? query.OrderByDescending(x => x.Reputation)
            : query.OrderBy(x => x.DisplayName);

        var profiles = await query
            .Select(x => ToDto(x))
            .ToListAsync();
        logger.LogDebug("Returned {Count} profiles", profiles.Count);

        return Ok(profiles);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ProfileDto>> GetProfile(string? id)
    {
        var profile = await db.UserProfiles.FindAsync(id);

        if (profile is null)
        {
            logger.LogDebug("Profile not found: {ProfileId}", id);
            return NotFound();
        }

        return Ok(ToDto(profile));
    }

    [HttpPut("edit")]
    [Authorize]
    public async Task<IActionResult> EditProfile(EditProfileDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
        {
            logger.LogWarning("Profile edit attempted without user ID");
            return Unauthorized();
        }

        var profile = await db.UserProfiles.FindAsync(userId);
        if (profile is null)
        {
            logger.LogWarning("Profile edit failed: Profile not found for user {UserId}", userId);
            return NotFound();
        }

        profile.DisplayName = dto.DisplayName ?? profile.DisplayName;
        profile.Description = dto.Description ?? profile.Description;
        profile.AvatarUrl = dto.AvatarUrl ?? profile.AvatarUrl;
        await db.SaveChangesAsync();

        logger.LogInformation("Profile updated: {UserId}, DisplayName={DisplayName}",
            userId, profile.DisplayName);

        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<ProfileDto>> GetMe()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
        {
            logger.LogWarning("Get profile attempted without user ID");
            return Unauthorized();
        }

        var profile = await db.UserProfiles.FindAsync(userId);

        if (profile is null)
        {
            logger.LogWarning("Profile not found for authenticated user {UserId}", userId);
            return NotFound();
        }

        return Ok(ToDto(profile));
    }

    [HttpGet("batch")]
    public async Task<ActionResult<List<ProfileSummaryDto>>> GetBatch(string ids)
    {
        var list = ids.Split(",", StringSplitOptions.RemoveEmptyEntries).Distinct().ToList();
        logger.LogDebug("Fetching batch of {Count} profiles", list.Count);

        var rows = await db.UserProfiles
            .Where(x => list.Contains(x.Id))
            .Select(x => new ProfileSummaryDto(x.Id, x.DisplayName, x.Reputation, x.AvatarUrl))
            .ToListAsync();

        logger.LogDebug("Batch fetch returned {Count} profiles", rows.Count);
        return Ok(rows);
    }

    private static ProfileDto ToDto(UserProfile p) =>
        new(p.Id, p.DisplayName, p.Description, p.AvatarUrl, p.Reputation, p.JoinedAt);
}