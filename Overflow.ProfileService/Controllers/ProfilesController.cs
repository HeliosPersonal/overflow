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
public class ProfilesController(ProfileDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<UserProfile>>> GetProfiles(string? sortBy)
    {
        var query = db.UserProfiles.AsQueryable();

        query = sortBy == "reputation"
            ? query.OrderByDescending(x => x.Reputation)
            : query.OrderBy(x => x.DisplayName);

        return await query.ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<List<UserProfile>>> GetProfile(string? id)
    {
        var profile = await db.UserProfiles.FindAsync(id);

        return profile is null ? NotFound() : Ok(profile);
    }

    [HttpPut("edit")]
    [Authorize]
    public async Task<ActionResult<List<UserProfile>>> EditProfile(EditProfileDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        var profile = await db.UserProfiles.FindAsync(userId);
        if (profile is null) return NotFound();

        profile.DisplayName = dto.DisplayName ?? profile.DisplayName;
        profile.Description = dto.Description ?? profile.Description;

        await db.SaveChangesAsync();

        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserProfile>> GetMe()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
        {
            return Unauthorized();
        }

        var profile = await db.UserProfiles.FindAsync(userId);
        return profile is null ? NotFound() : Ok(profile);
    }

    [HttpGet("batch")]
    public async Task<ActionResult<UserProfile>> GetBatch(string ids)
    {
        var list = ids.Split(",", StringSplitOptions.RemoveEmptyEntries).Distinct().ToList();

        var rows = await db.UserProfiles
            .Where(x => list.Contains(x.Id))
            .Select(x => new ProfileSummaryDto(x.Id, x.DisplayName, x.Reputation))
            .ToListAsync();

        return Ok(rows);
    }

    /// <summary>
    /// Create a new user profile.
    /// If authenticated, uses the Keycloak user ID from the JWT token.
    /// Otherwise, generates a new ID (for backward compatibility/testing).
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<UserProfile>> CreateProfile(CreateProfileDto dto)
    {
        // Get user ID from JWT token if authenticated
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        
        // Check if profile already exists
        if (!string.IsNullOrEmpty(userId))
        {
            var existingProfile = await db.UserProfiles.FindAsync(userId);
            if (existingProfile != null)
            {
                return Conflict(new { message = "Profile already exists for this user" });
            }
        }

        var profile = new UserProfile
        {
            Id = userId ?? Guid.NewGuid().ToString(),
            DisplayName = dto.DisplayName,
            Description = dto.Description
        };

        db.UserProfiles.Add(profile);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetProfile), new { id = profile.Id }, profile);
    }
}