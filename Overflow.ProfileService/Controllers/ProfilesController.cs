using System.Security.Claims;
using CommandFlow;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Overflow.ProfileService.DTOs;
using Overflow.ProfileService.Features.Profiles.Commands;
using Overflow.ProfileService.Features.Profiles.Queries;

namespace Overflow.ProfileService.Controllers;

[ApiController]
[Route("[controller]")]
public class ProfilesController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ProfileDto>>> GetProfiles(string? sortBy)
    {
        var profiles = await sender.Send(new GetProfilesQuery(sortBy));
        return Ok(profiles);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ProfileDto>> GetProfile(string id)
    {
        var profile = await sender.Send(new GetProfileByIdQuery(id));
        return profile is null ? NotFound() : Ok(profile);
    }

    [HttpPut("edit")]
    [Authorize]
    public async Task<IActionResult> EditProfile(EditProfileDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        var result = await sender.Send(new EditProfileCommand(userId, dto.DisplayName, dto.Description, dto.AvatarUrl,
            dto.ThemePreference));
        return result.IsSuccess ? NoContent() : NotFound(result.Error);
    }

    [HttpPut("theme")]
    [Authorize]
    public async Task<IActionResult> UpdateTheme([FromBody] UpdateThemeDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        var result = await sender.Send(new UpdateThemePreferenceCommand(userId, dto.ThemePreference));
        return result.IsSuccess ? NoContent() : NotFound(result.Error);
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<ProfileDto>> GetMe()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        var profile = await sender.Send(new GetProfileByIdQuery(userId));
        return profile is null ? NotFound() : Ok(profile);
    }

    [HttpGet("batch")]
    public async Task<ActionResult<List<ProfileSummaryDto>>> GetBatch(string ids)
    {
        var list = ids.Split(",", StringSplitOptions.RemoveEmptyEntries).Distinct().ToList();
        var rows = await sender.Send(new GetProfileBatchQuery(list));
        return Ok(rows);
    }
}