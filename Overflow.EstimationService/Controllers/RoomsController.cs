using CommandFlow;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Overflow.Common;
using Overflow.Common.CommonExtensions;
using Overflow.EstimationService.Auth;
using Overflow.EstimationService.DTOs;
using Overflow.EstimationService.Extensions;
using Overflow.EstimationService.Features.Rooms.Commands;
using Overflow.EstimationService.Features.Rooms.Queries;
using Overflow.EstimationService.Options;

namespace Overflow.EstimationService.Controllers;

[ApiController]
[Route("estimation")]
public class RoomsController(
    ISender sender,
    IdentityResolver identityResolver,
    IOptions<RoomCleanupOptions> cleanupOptions,
    IConfiguration configuration) : ControllerBase
{
    private string BaseUrl => configuration[ConfigurationKeys.AppBaseUrl] ?? "http://localhost:3000";
    private int ArchivedDaysBeforeDelete => cleanupOptions.Value.ArchivedDaysBeforeDelete;
    private int InactiveDaysBeforeArchive => cleanupOptions.Value.InactiveDaysBeforeArchive;

    private string? AccessToken
    {
        get
        {
            var raw = HttpContext.Request.Headers.Authorization.ToString().Replace("Bearer ", "");
            return string.IsNullOrWhiteSpace(raw) ? null : raw;
        }
    }

    // ─── POST /estimation/claim-guest ────────────────────────────────────

    [Authorize]
    [HttpPost("claim-guest")]
    public async Task<IActionResult> ClaimGuest()
    {
        var userId = User.GetRequiredUserId();

        var identity = await identityResolver.ResolveAsync(HttpContext);
        var guestId = GuestIdentity.GetGuestId(HttpContext);

        var claimed = await sender.Send(new ClaimGuestCommand(userId, guestId, identity.DisplayName));

        if (!string.IsNullOrEmpty(guestId))
            HttpContext.Response.Cookies.Delete(GuestIdentity.CookieName);

        return Ok(new { claimed });
    }

    // ─── GET /estimation/rooms/my ────────────────────────────────────────

    [Authorize]
    [HttpGet("rooms/my")]
    public async Task<IActionResult> GetMyRooms()
    {
        var userId = User.GetRequiredUserId();

        var summaries =
            await sender.Send(new GetMyRoomsQuery(userId, ArchivedDaysBeforeDelete, InactiveDaysBeforeArchive,
                AccessToken));
        return Ok(summaries);
    }

    private const int MaxRoomTitleLength = 80;

    // ─── POST /estimation/rooms ──────────────────────────────────────────

    [HttpPost("rooms")]
    public async Task<IActionResult> CreateRoom([FromBody] CreateRoomRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Title))
            return BadRequest("Room title is required");

        if (req.Title.Trim().Length > MaxRoomTitleLength)
            return BadRequest($"Room title must be {MaxRoomTitleLength} characters or fewer");

        var identity = await identityResolver.ResolveAsync(HttpContext, req.DisplayName);

        if (identity.IsGuest && string.IsNullOrWhiteSpace(req.DisplayName))
            return BadRequest("Display name is required for guest users");

        var result = await sender.Send(new CreateRoomCommand(
            req.Title, req.DeckType, req.Tasks, identity, BaseUrl, AccessToken));

        return result.IsSuccess
            ? Created($"/estimation/rooms/{result.Value.RoomId}", result.Value)
            : result.Error.ToActionResult();
    }

    // ─── POST /estimation/rooms/{roomId}/join ────────────────────────────

    [HttpPost("rooms/{roomId:guid}/join")]
    public async Task<IActionResult> JoinRoom(Guid roomId, [FromBody] JoinRoomRequest? req)
    {
        var identity = await identityResolver.ResolveAsync(HttpContext, req?.DisplayName);

        var result = await sender.Send(new JoinRoomCommand(
            roomId, req?.DisplayName, identity, BaseUrl, AccessToken));

        return result.IsSuccess
            ? Ok(result.Value)
            : result.Error.ToActionResult();
    }

    // ─── GET /estimation/rooms/{roomId} ──────────────────────────────────

    [HttpGet("rooms/{roomId:guid}")]
    public async Task<IActionResult> GetRoom(Guid roomId)
    {
        var identity = await identityResolver.ResolveAsync(HttpContext);

        var response = await sender.Send(new GetRoomQuery(roomId, identity.ParticipantId, BaseUrl, AccessToken));
        return response is null ? NotFound("Room not found") : Ok(response);
    }

    // ─── POST /estimation/rooms/{roomId}/mode ────────────────────────────

    [HttpPost("rooms/{roomId:guid}/mode")]
    public async Task<IActionResult> ChangeMode(Guid roomId, [FromBody] ChangeModeRequest req)
    {
        var identity = await identityResolver.ResolveAsync(HttpContext);

        var result = await sender.Send(new ChangeModeCommand(
            roomId, req.IsSpectator, identity.ParticipantId, BaseUrl, AccessToken));

        return result.IsSuccess ? Ok(result.Value) : result.Error.ToActionResult();
    }

    // ─── POST /estimation/rooms/{roomId}/leave ───────────────────────────

    [HttpPost("rooms/{roomId:guid}/leave")]
    public async Task<IActionResult> LeaveRoom(Guid roomId)
    {
        var identity = await identityResolver.ResolveAsync(HttpContext);

        var result = await sender.Send(new LeaveRoomCommand(roomId, identity.ParticipantId));
        return result.IsSuccess ? NoContent() : result.Error.ToActionResult();
    }

    // ─── POST /estimation/rooms/{roomId}/votes ───────────────────────────

    [HttpPost("rooms/{roomId:guid}/votes")]
    public async Task<IActionResult> SubmitVote(Guid roomId, [FromBody] SubmitVoteRequest req)
    {
        var identity = await identityResolver.ResolveAsync(HttpContext);

        var result = await sender.Send(new SubmitVoteCommand(
            roomId, req.Value, identity.ParticipantId, BaseUrl, AccessToken));

        return result.IsSuccess ? Ok(result.Value) : result.Error.ToActionResult();
    }

    // ─── DELETE /estimation/rooms/{roomId}/votes/me ──────────────────────

    [HttpDelete("rooms/{roomId:guid}/votes/me")]
    public async Task<IActionResult> ClearVote(Guid roomId)
    {
        var identity = await identityResolver.ResolveAsync(HttpContext);

        var result = await sender.Send(new ClearVoteCommand(roomId, identity.ParticipantId));
        return result.IsSuccess ? NoContent() : result.Error.ToActionResult();
    }

    // ─── POST /estimation/rooms/{roomId}/reveal ──────────────────────────

    [Authorize]
    [HttpPost("rooms/{roomId:guid}/reveal")]
    public async Task<IActionResult> RevealVotes(Guid roomId)
    {
        var userId = User.GetRequiredUserId();

        var result = await sender.Send(new RevealVotesCommand(roomId, userId, BaseUrl, AccessToken));
        return result.IsSuccess ? Ok(result.Value) : result.Error.ToActionResult();
    }

    // ─── POST /estimation/rooms/{roomId}/reset ───────────────────────────

    [Authorize]
    [HttpPost("rooms/{roomId:guid}/reset")]
    public async Task<IActionResult> ResetRound(Guid roomId)
    {
        var userId = User.GetRequiredUserId();

        var result = await sender.Send(new ResetRoundCommand(roomId, userId, BaseUrl, AccessToken));
        return result.IsSuccess ? Ok(result.Value) : result.Error.ToActionResult();
    }

    // ─── POST /estimation/rooms/{roomId}/revote ──────────────────────────

    [Authorize]
    [HttpPost("rooms/{roomId:guid}/revote")]
    public async Task<IActionResult> Revote(Guid roomId, [FromBody] RevoteRequest? req = null)
    {
        var userId = User.GetRequiredUserId();

        var result = await sender.Send(new RevoteCommand(roomId, userId, req?.RoundNumber, BaseUrl, AccessToken));
        return result.IsSuccess ? Ok(result.Value) : result.Error.ToActionResult();
    }

    // ─── PUT /estimation/rooms/{roomId}/tasks ────────────────────────────

    [Authorize]
    [HttpPut("rooms/{roomId:guid}/tasks")]
    public async Task<IActionResult> UpdateTasks(Guid roomId, [FromBody] UpdateTasksRequest req)
    {
        var userId = User.GetRequiredUserId();

        var result = await sender.Send(new UpdateTasksCommand(roomId, userId, req.Tasks, BaseUrl, AccessToken));
        return result.IsSuccess ? Ok(result.Value) : result.Error.ToActionResult();
    }

    // ─── POST /estimation/rooms/{roomId}/archive ─────────────────────────

    [Authorize]
    [HttpPost("rooms/{roomId:guid}/archive")]
    public async Task<IActionResult> ArchiveRoom(Guid roomId)
    {
        var userId = User.GetRequiredUserId();

        var result = await sender.Send(new ArchiveRoomCommand(roomId, userId, BaseUrl, AccessToken));
        return result.IsSuccess ? Ok(result.Value) : result.Error.ToActionResult();
    }

    // ─── PUT /estimation/rooms/{roomId}/title ────────────────────────────

    [Authorize]
    [HttpPut("rooms/{roomId:guid}/title")]
    public async Task<IActionResult> RenameRoom(Guid roomId, [FromBody] RenameRoomRequest req)
    {
        var userId = User.GetRequiredUserId();

        var result = await sender.Send(new RenameRoomCommand(roomId, userId, req.Title, BaseUrl, AccessToken));
        return result.IsSuccess ? Ok(result.Value) : result.Error.ToActionResult();
    }

    // ─── DELETE /estimation/rooms/{roomId} ───────────────────────────────

    [Authorize]
    [HttpDelete("rooms/{roomId:guid}")]
    public async Task<IActionResult> DeleteRoom(Guid roomId)
    {
        var userId = User.GetRequiredUserId();

        var result = await sender.Send(new DeleteRoomCommand(roomId, userId));
        return result.IsSuccess ? NoContent() : result.Error.ToActionResult();
    }
}