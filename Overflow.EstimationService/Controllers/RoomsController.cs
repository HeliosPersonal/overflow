using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Overflow.EstimationService.Auth;
using Overflow.EstimationService.Clients;
using Overflow.EstimationService.DTOs;
using Overflow.EstimationService.Exceptions;
using Overflow.EstimationService.Mapping;
using Overflow.EstimationService.Models;
using Overflow.EstimationService.Options;
using Overflow.EstimationService.Services;

namespace Overflow.EstimationService.Controllers;

[ApiController]
[Route("estimation")]
public class RoomsController(
    EstimationRoomService svc,
    IdentityResolver identityResolver,
    ProfileServiceClient profileClient,
    IOptions<RoomCleanupOptions> cleanupOptions,
    IConfiguration configuration) : ControllerBase
{
    private string BaseUrl => configuration["APP_BASE_URL"] ?? "http://localhost:3000";
    private int RetentionDays => cleanupOptions.Value.RetentionDays;

    // ─── POST /estimation/claim-guest ────────────────────────────────────

    [Authorize]
    [HttpPost("claim-guest")]
    public async Task<IActionResult> ClaimGuest()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        var guestId = GuestIdentity.GetGuestId(HttpContext);
        if (string.IsNullOrEmpty(guestId))
            return Ok(new { claimed = 0 });

        var identity = await identityResolver.ResolveAsync(HttpContext);
        var claimed = await svc.ClaimGuestAsync(guestId, userId, identity.DisplayName);

        // Clear the guest cookie — the user is now fully authenticated
        HttpContext.Response.Cookies.Delete(GuestIdentity.CookieName);

        return Ok(new { claimed });
    }

    // ─── GET /estimation/rooms/my ────────────────────────────────────────

    [Authorize]
    [HttpGet("rooms/my")]
    public async Task<IActionResult> GetMyRooms()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        var rooms = await svc.GetRoomsForUserAsync(userId);

        // Collect all unique user IDs from participants to resolve avatars in a single batch
        var allUserIds = rooms
            .SelectMany(r => r.Participants)
            .Where(p => p.UserId is not null)
            .Select(p => p.UserId!)
            .Distinct()
            .ToList();

        var avatarLookup = await ResolveAvatarsAsync(allUserIds);

        var summaries = rooms
            .OrderBy(r => r.Status)
            .ThenByDescending(r => r.CreatedAtUtc)
            .Select(r =>
            {
                var creator = r.Participants.FirstOrDefault(p => p.IsModerator)
                              ?? r.Participants.FirstOrDefault();
                return new RoomSummaryResponse(
                    r.Id,
                    r.Title,
                    r.Status,
                    r.RoundNumber,
                    r.Participants.Count,
                    r.RoundHistory.Count,
                    r.CreatedAtUtc,
                    r.ArchivedAtUtc,
                    r.ModeratorUserId == userId,
                    RetentionDays,
                    creator?.DisplayName ?? "Unknown",
                    creator?.UserId is not null ? avatarLookup.GetValueOrDefault(creator.UserId) : null,
                    r.Participants
                        .Select(p => new ParticipantSummaryResponse(
                            p.DisplayName,
                            p.UserId is not null ? avatarLookup.GetValueOrDefault(p.UserId) : null))
                        .ToList()
                );
            }).ToList();

        return Ok(summaries);
    }

    // ─── POST /estimation/rooms ──────────────────────────────────────────

    [HttpPost("rooms")]
    public async Task<IActionResult> CreateRoom([FromBody] CreateRoomRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Title))
            return BadRequest("Room title is required");

        if (req.Title.Trim().Length > 80)
            return BadRequest("Room title must be 80 characters or fewer");

        var identity = await identityResolver.ResolveAsync(HttpContext, req.DisplayName);

        if (identity.IsGuest && string.IsNullOrWhiteSpace(req.DisplayName))
            return BadRequest("Display name is required for guest users");

        var result = await svc.CreateRoomAsync(req.Title, identity.ParticipantId,
            identity.UserId, identity.GuestId, identity.DisplayName, identity.IsGuest, req.DeckType);

        if (!result.IsSuccess)
            return result.Error.ToActionResult();

        var avatarLookup = await ResolveAvatarsForRoomAsync(result.Value);
        return Created($"/estimation/rooms/{result.Value.Id}",
            RoomResponseMapper.ToResponse(result.Value, identity.ParticipantId, BaseUrl, avatarLookup));
    }

    // ─── POST /estimation/rooms/{roomId}/join ──────────────────────────────

    [HttpPost("rooms/{roomId:guid}/join")]
    public async Task<IActionResult> JoinRoom(Guid roomId, [FromBody] JoinRoomRequest? req)
    {
        var identity = await identityResolver.ResolveAsync(HttpContext, req?.DisplayName);

        if (identity.IsGuest && string.IsNullOrWhiteSpace(req?.DisplayName))
        {
            var existingRoom = await svc.GetRoomByIdAsync(roomId);
            var existingParticipant = existingRoom?.Participants
                .FirstOrDefault(p => p.ParticipantId == identity.ParticipantId);
            if (existingParticipant is null)
                return BadRequest("Display name is required for guest participants");
            identity = identity with { DisplayName = existingParticipant.DisplayName };
        }

        var result = await svc.JoinRoomAsync(roomId, identity.ParticipantId, identity.UserId,
            identity.GuestId, identity.DisplayName, identity.IsGuest);

        if (!result.IsSuccess)
            return result.Error.ToActionResult();

        var avatarLookup = await ResolveAvatarsForRoomAsync(result.Value);
        return Ok(RoomResponseMapper.ToResponse(result.Value, identity.ParticipantId, BaseUrl, avatarLookup));
    }

    // ─── GET /estimation/rooms/{roomId} ────────────────────────────────────

    [HttpGet("rooms/{roomId:guid}")]
    public async Task<IActionResult> GetRoom(Guid roomId)
    {
        var room = await svc.GetRoomByIdAsync(roomId);
        if (room is null) return NotFound("Room not found");

        var identity = await identityResolver.ResolveAsync(HttpContext);
        var avatarLookup = await ResolveAvatarsForRoomAsync(room);
        return Ok(RoomResponseMapper.ToResponse(room, identity.ParticipantId, BaseUrl, avatarLookup));
    }

    // ─── POST /estimation/rooms/{roomId}/mode ──────────────────────────────

    [HttpPost("rooms/{roomId:guid}/mode")]
    public async Task<IActionResult> ChangeMode(Guid roomId, [FromBody] ChangeModeRequest req)
    {
        var identity = await identityResolver.ResolveAsync(HttpContext);

        var result = await svc.ChangeModeAsync(roomId, identity.ParticipantId, req.IsSpectator);
        if (!result.IsSuccess)
            return result.Error.ToActionResult();

        var avatarLookup = await ResolveAvatarsForRoomAsync(result.Value);
        return Ok(RoomResponseMapper.ToResponse(result.Value, identity.ParticipantId, BaseUrl, avatarLookup));
    }

    // ─── POST /estimation/rooms/{roomId}/leave ─────────────────────────────

    [HttpPost("rooms/{roomId:guid}/leave")]
    public async Task<IActionResult> LeaveRoom(Guid roomId)
    {
        var identity = await identityResolver.ResolveAsync(HttpContext);

        var result = await svc.LeaveRoomAsync(roomId, identity.ParticipantId);
        return result.IsSuccess
            ? NoContent()
            : result.Error.ToActionResult();
    }

    // ─── POST /estimation/rooms/{roomId}/votes ─────────────────────────────

    [HttpPost("rooms/{roomId:guid}/votes")]
    public async Task<IActionResult> SubmitVote(Guid roomId, [FromBody] SubmitVoteRequest req)
    {
        var identity = await identityResolver.ResolveAsync(HttpContext);

        var result = await svc.SubmitVoteAsync(roomId, identity.ParticipantId, req.Value);
        if (!result.IsSuccess)
            return result.Error.ToActionResult();

        var avatarLookup = await ResolveAvatarsForRoomAsync(result.Value);
        return Ok(RoomResponseMapper.ToResponse(result.Value, identity.ParticipantId, BaseUrl, avatarLookup));
    }

    // ─── DELETE /estimation/rooms/{roomId}/votes/me ────────────────────────

    [HttpDelete("rooms/{roomId:guid}/votes/me")]
    public async Task<IActionResult> ClearVote(Guid roomId)
    {
        var identity = await identityResolver.ResolveAsync(HttpContext);

        var result = await svc.ClearVoteAsync(roomId, identity.ParticipantId);
        return result.IsSuccess
            ? NoContent()
            : result.Error.ToActionResult();
    }

    // ─── POST /estimation/rooms/{roomId}/reveal ────────────────────────────

    [Authorize]
    [HttpPost("rooms/{roomId:guid}/reveal")]
    public async Task<IActionResult> RevealVotes(Guid roomId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        var result = await svc.RevealVotesAsync(roomId, userId);
        if (!result.IsSuccess)
            return result.Error.ToActionResult();

        var avatarLookup = await ResolveAvatarsForRoomAsync(result.Value);
        return Ok(RoomResponseMapper.ToResponse(result.Value, userId, BaseUrl, avatarLookup));
    }

    // ─── POST /estimation/rooms/{roomId}/reset ─────────────────────────────

    [Authorize]
    [HttpPost("rooms/{roomId:guid}/reset")]
    public async Task<IActionResult> ResetRound(Guid roomId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        var result = await svc.ResetRoundAsync(roomId, userId);
        if (!result.IsSuccess)
            return result.Error.ToActionResult();

        var avatarLookup = await ResolveAvatarsForRoomAsync(result.Value);
        return Ok(RoomResponseMapper.ToResponse(result.Value, userId, BaseUrl, avatarLookup));
    }

    // ─── POST /estimation/rooms/{roomId}/revote ─────────────────────────────

    [Authorize]
    [HttpPost("rooms/{roomId:guid}/revote")]
    public async Task<IActionResult> Revote(Guid roomId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        var result = await svc.RevoteAsync(roomId, userId);
        if (!result.IsSuccess)
            return result.Error.ToActionResult();

        var avatarLookup = await ResolveAvatarsForRoomAsync(result.Value);
        return Ok(RoomResponseMapper.ToResponse(result.Value, userId, BaseUrl, avatarLookup));
    }

    // ─── POST /estimation/rooms/{roomId}/archive ───────────────────────────

    [Authorize]
    [HttpPost("rooms/{roomId:guid}/archive")]
    public async Task<IActionResult> ArchiveRoom(Guid roomId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        var result = await svc.ArchiveRoomAsync(roomId, userId);
        if (!result.IsSuccess)
            return result.Error.ToActionResult();

        var avatarLookup = await ResolveAvatarsForRoomAsync(result.Value);
        return Ok(RoomResponseMapper.ToResponse(result.Value, userId, BaseUrl, avatarLookup));
    }

    // ─── DELETE /estimation/rooms/{roomId} ─────────────────────────────────

    [Authorize]
    [HttpDelete("rooms/{roomId:guid}")]
    public async Task<IActionResult> DeleteRoom(Guid roomId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        var result = await svc.DeleteRoomAsync(roomId, userId);
        return result.IsSuccess
            ? NoContent()
            : result.Error.ToActionResult();
    }

    // ─── Avatar resolution helpers ──────────────────────────────────────

    /// <summary>
    /// Resolves avatar URLs for all authenticated participants in a room.
    /// Uses ProfileServiceClient (FusionCache: 60s L1 + L2 Redis) so repeated
    /// calls for the same user within a request are essentially free.
    /// </summary>
    private async Task<Dictionary<string, string?>> ResolveAvatarsForRoomAsync(EstimationRoom room)
    {
        var userIds = room.Participants
            .Where(p => p.UserId is not null)
            .Select(p => p.UserId!)
            .Distinct()
            .ToList();

        return await ResolveAvatarsAsync(userIds);
    }

    private async Task<Dictionary<string, string?>> ResolveAvatarsAsync(IList<string> userIds)
    {
        var result = new Dictionary<string, string?>();
        var accessToken = HttpContext.Request.Headers.Authorization.ToString().Replace("Bearer ", "");
        var token = string.IsNullOrWhiteSpace(accessToken) ? null : accessToken;

        // ProfileServiceClient uses FusionCache internally — each call is cached for 60s.
        // For rooms with many participants, this fans out to parallel cache lookups (fast).
        await Task.WhenAll(userIds.Select(async userId =>
        {
            var profile = await profileClient.GetProfileDataAsync(userId, token);
            lock (result)
            {
                result[userId] = profile?.AvatarUrl;
            }
        }));

        return result;
    }
}

/// <summary>
/// Maps <see cref="RoomError"/> to the appropriate HTTP status code.
/// </summary>
internal static class RoomErrorExtensions
{
    public static IActionResult ToActionResult(this RoomError error) => error.Code switch
    {
        RoomErrorCode.NotFound => new NotFoundObjectResult(error.Message),
        RoomErrorCode.Forbidden => new ForbidResult(),
        RoomErrorCode.Archived or
            RoomErrorCode.ParticipantNotFound or
            RoomErrorCode.InvalidState or
            RoomErrorCode.InvalidVote or
            _ => new BadRequestObjectResult(error.Message)
    };
}