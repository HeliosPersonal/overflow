using System.Security.Claims;
using CommandFlow;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Overflow.EstimationService.Features.Rooms.Commands;

namespace Overflow.EstimationService.Controllers;

/// <summary>
/// Manages the EstimationService's local profile cache (FusionCache).
/// EstimationService caches profile data (display name + avatar) from ProfileService
/// to avoid per-request HTTP calls. This controller exposes cache invalidation
/// so callers (e.g. the webapp's editProfile action) can bust stale entries
/// after a profile or avatar edit.
/// </summary>
[ApiController]
[Route("estimation")]
[Authorize]
public class ProfileCacheController(ISender sender) : ControllerBase
{
    /// <summary>
    /// Evicts the cached profile for the current user so subsequent reads
    /// (WebSocket broadcasts, room listings) fetch fresh data from ProfileService.
    /// No DB writes — just cache invalidation. Call after profile/avatar edits.
    /// </summary>
    [HttpDelete("profile-cache")]
    public async Task<IActionResult> InvalidateProfile()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        await sender.Send(new InvalidateProfileCacheCommand(userId));
        return Ok(new { invalidated = true });
    }
}