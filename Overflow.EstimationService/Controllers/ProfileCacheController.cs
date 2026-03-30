using CommandFlow;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Overflow.Common.CommonExtensions;
using Overflow.EstimationService.Features.Rooms.Commands;

namespace Overflow.EstimationService.Controllers;

/// <summary>
/// Exposes cache invalidation for EstimationService's local profile cache.
/// Called by the webapp's editProfile action after profile/avatar edits.
/// </summary>
[ApiController]
[Route("estimation")]
[Authorize]
public class ProfileCacheController(ISender sender) : ControllerBase
{
    [HttpDelete("profile-cache")]
    public async Task<IActionResult> InvalidateProfile()
    {
        var userId = User.GetRequiredUserId();
        await sender.Send(new InvalidateProfileCacheCommand(userId));
        return Ok(new { invalidated = true });
    }
}