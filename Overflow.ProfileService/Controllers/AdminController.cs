using CommandFlow;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Overflow.Common;
using Overflow.Common.CommonExtensions;
using Overflow.Contracts;
using Overflow.ProfileService.DTOs;
using Overflow.ProfileService.Features.Profiles.Commands;
using Overflow.ProfileService.Features.Profiles.Queries;
using Wolverine;

namespace Overflow.ProfileService.Controllers;

[ApiController]
[Route("profiles/admin")]
[Authorize(Roles = "admin")]
public class AdminController(ISender sender, IMessageBus bus, ILogger<AdminController> logger) : ControllerBase
{
    public record AdminUpdateUserDto(string? DisplayName, string? Description, string? AvatarUrl);
    public record BulkDeleteDto(List<string> UserIds);

    [HttpGet("users")]
    public async Task<ActionResult<PaginationResult<ProfileDto>>> GetUsers(
        [FromQuery] string? search, [FromQuery] int? page, [FromQuery] int? pageSize)
    {
        var result = await sender.Send(new GetAdminUsersQuery(search, page, pageSize));
        return Ok(result);
    }

    [HttpPut("{userId}")]
    public async Task<IActionResult> UpdateUser(string userId, AdminUpdateUserDto dto)
    {
        var adminId = User.GetRequiredUserId();
        logger.LogInformation("Admin {AdminId} updating user {UserId}: DisplayName={DisplayName}, Description={Description}, AvatarUrl={AvatarUrl}",
            adminId, userId, dto.DisplayName, dto.Description != null ? "(set)" : "(unchanged)", dto.AvatarUrl != null ? "(set)" : "(unchanged)");

        var result = await sender.Send(new AdminUpdateUserCommand(userId, dto.DisplayName, dto.Description, dto.AvatarUrl));
        if (result.IsFailure)
        {
            logger.LogWarning("Admin {AdminId} update failed for user {UserId}: {Error}", adminId, userId, result.Error);
            return NotFound(result.Error);
        }

        logger.LogInformation("Admin {AdminId} successfully updated user {UserId}", adminId, userId);
        return Ok(result.Value);
    }

    [HttpDelete("{userId}")]
    public async Task<IActionResult> DeleteUser(string userId)
    {
        var adminId = User.GetRequiredUserId();
        logger.LogInformation("Admin {AdminId} deleting user {UserId}", adminId, userId);
        await bus.PublishAsync(new UserDeleted(userId));
        return Accepted();
    }

    [HttpPost("bulk-delete")]
    public async Task<IActionResult> BulkDelete(BulkDeleteDto dto)
    {
        var adminId = User.GetRequiredUserId();
        logger.LogInformation("Admin {AdminId} bulk-deleting {Count} user(s): [{UserIds}]",
            adminId, dto.UserIds.Count, string.Join(", ", dto.UserIds));

        foreach (var userId in dto.UserIds)
        {
            await bus.PublishAsync(new UserDeleted(userId));
        }

        return Accepted();
    }
}
