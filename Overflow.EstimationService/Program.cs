using System.Net.WebSockets;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using JasperFx.Events;
using JasperFx.Events.Projections;
using Marten;
using Overflow.Common.CommonExtensions;
using Overflow.EstimationService.DTOs;
using Overflow.EstimationService.Events;
using Overflow.EstimationService.Models;
using Overflow.EstimationService.Projections;
using Overflow.EstimationService.Services;
using Overflow.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddEnvVariablesAndConfigureSecrets();
builder.ConfigureKeycloakFromSettings();

builder.Services.AddOpenApi();
builder.AddServiceDefaults();
builder.AddKeyCloakAuthentication();

builder.Services.AddScoped<EstimationRoomService>();
builder.Services.AddSingleton<WebSocketRoomManager>();

var connString = builder.Configuration.GetConnectionString("estimationDb")!;

builder.Services.AddHealthChecks();

builder.Services.AddMarten(opts =>
{
    opts.Connection(connString);
    opts.Events.StreamIdentity = StreamIdentity.AsGuid;

    opts.Events.AddEventType<RoomCreated>();
    opts.Events.AddEventType<ParticipantJoined>();
    opts.Events.AddEventType<ParticipantModeChanged>();
    opts.Events.AddEventType<ParticipantLeft>();
    opts.Events.AddEventType<VoteSubmitted>();
    opts.Events.AddEventType<VoteCleared>();
    opts.Events.AddEventType<VotesRevealed>();
    opts.Events.AddEventType<RoundReset>();
    opts.Events.AddEventType<RoomArchived>();

    opts.Schema.For<EstimationRoomView>()
        .Index(x => x.Code, i => i.IsUnique = true);

    opts.Projections.Add(new EstimationRoomProjection(), ProjectionLifecycle.Inline);
}).UseLightweightSessions();

var app = builder.Build();

if (!app.Environment.IsProduction())
{
    app.MapOpenApi();
}

app.UseAuthentication();
app.UseAuthorization();
app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(30) });

var baseUrl = builder.Configuration["APP_BASE_URL"] ?? "http://localhost:3000";

var jsonOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
};

// ─── Helper: resolve participant identity ────────────────────────────────

static (string participantId, string? userId, string? guestId, string displayName, bool isGuest) ResolveIdentity(
    HttpContext ctx, string? guestDisplayName = null)
{
    var userId = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier);
    if (userId is not null)
    {
        var name = ctx.User.FindFirstValue("preferred_username")
                   ?? ctx.User.FindFirstValue(ClaimTypes.Name)
                   ?? "User";
        return (userId, userId, null, name, false);
    }

    var guestId = GuestIdentity.GetGuestId(ctx);
    if (!string.IsNullOrEmpty(guestId))
    {
        return (guestId, null, guestId, guestDisplayName ?? "Guest", true);
    }

    // New guest — issue cookie
    var newGuestId = GuestIdentity.EnsureGuestId(ctx);
    return (newGuestId, null, newGuestId, guestDisplayName ?? "Guest", true);
}

// ─── POST /estimation/rooms ──────────────────────────────────────────────

app.MapPost("/estimation/rooms", async (
    CreateRoomRequest req,
    EstimationRoomService svc,
    HttpContext ctx) =>
{
    var userId = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier);
    if (userId is null) return Results.Unauthorized();

    if (string.IsNullOrWhiteSpace(req.Title))
        return Results.BadRequest("Room title is required");

    var displayName = ctx.User.FindFirstValue("preferred_username")
                      ?? ctx.User.FindFirstValue(ClaimTypes.Name)
                      ?? "Moderator";

    var room = await svc.CreateRoomAsync(req.Title, userId, displayName, req.DeckType);
    var response = RoomResponseMapper.ToResponse(room, userId, baseUrl);
    return Results.Created($"/estimation/rooms/{room.Code}", response);
}).RequireAuthorization();

// ─── POST /estimation/rooms/{code}/join ──────────────────────────────────

app.MapPost("/estimation/rooms/{code}/join", async (
    string code,
    JoinRoomRequest? req,
    EstimationRoomService svc,
    HttpContext ctx) =>
{
    var (participantId, userId, guestId, displayName, isGuest) =
        ResolveIdentity(ctx, req?.DisplayName);

    if (isGuest && string.IsNullOrWhiteSpace(req?.DisplayName))
    {
        // Check if they are already a participant (rejoin)
        var existingRoom = await svc.GetRoomByCodeAsync(code);
        var existingParticipant = existingRoom?.Participants.FirstOrDefault(p => p.ParticipantId == participantId);
        if (existingParticipant is null)
            return Results.BadRequest("Display name is required for guest participants");
        displayName = existingParticipant.DisplayName;
    }

    try
    {
        var room = await svc.JoinRoomAsync(code, participantId, userId, guestId, displayName, isGuest);
        var response = RoomResponseMapper.ToResponse(room, participantId, baseUrl);
        return Results.Ok(response);
    }
    catch (RoomNotFoundException)
    {
        return Results.NotFound("Room not found");
    }
    catch (RoomArchivedException)
    {
        return Results.BadRequest("Room is archived");
    }
});

// ─── GET /estimation/rooms/{code} ────────────────────────────────────────

app.MapGet("/estimation/rooms/{code}", async (
    string code,
    EstimationRoomService svc,
    HttpContext ctx) =>
{
    var room = await svc.GetRoomByCodeAsync(code);
    if (room is null) return Results.NotFound("Room not found");

    var (participantId, _, _, _, _) = ResolveIdentity(ctx);
    var response = RoomResponseMapper.ToResponse(room, participantId, baseUrl);
    return Results.Ok(response);
});

// ─── POST /estimation/rooms/{code}/mode ──────────────────────────────────

app.MapPost("/estimation/rooms/{code}/mode", async (
    string code,
    ChangeModeRequest req,
    EstimationRoomService svc,
    WebSocketRoomManager wsManager,
    HttpContext ctx) =>
{
    var (participantId, _, _, _, _) = ResolveIdentity(ctx);

    try
    {
        var room = await svc.ChangeModeAsync(code, participantId, req.IsSpectator);
        var response = RoomResponseMapper.ToResponse(room, participantId, baseUrl);

        await BroadcastRoomUpdate(wsManager, room, code, baseUrl);
        return Results.Ok(response);
    }
    catch (RoomNotFoundException)
    {
        return Results.NotFound("Room not found");
    }
    catch (RoomArchivedException)
    {
        return Results.BadRequest("Room is archived");
    }
    catch (ParticipantNotFoundException)
    {
        return Results.BadRequest("Not a participant");
    }
});

// ─── POST /estimation/rooms/{code}/leave ─────────────────────────────────

app.MapPost("/estimation/rooms/{code}/leave", async (
    string code,
    EstimationRoomService svc,
    WebSocketRoomManager wsManager,
    HttpContext ctx) =>
{
    var (participantId, _, _, _, _) = ResolveIdentity(ctx);

    try
    {
        await svc.LeaveRoomAsync(code, participantId);
        var room = await svc.GetRoomByCodeAsync(code);
        if (room is not null) await BroadcastRoomUpdate(wsManager, room, code, baseUrl);
        return Results.NoContent();
    }
    catch (RoomNotFoundException)
    {
        return Results.NotFound("Room not found");
    }
});

// ─── POST /estimation/rooms/{code}/votes ─────────────────────────────────

app.MapPost("/estimation/rooms/{code}/votes", async (
    string code,
    SubmitVoteRequest req,
    EstimationRoomService svc,
    WebSocketRoomManager wsManager,
    HttpContext ctx) =>
{
    var (participantId, _, _, _, _) = ResolveIdentity(ctx);

    try
    {
        var room = await svc.SubmitVoteAsync(code, participantId, req.Value);
        var response = RoomResponseMapper.ToResponse(room, participantId, baseUrl);

        await BroadcastRoomUpdate(wsManager, room, code, baseUrl);
        return Results.Ok(response);
    }
    catch (RoomNotFoundException)
    {
        return Results.NotFound("Room not found");
    }
    catch (InvalidRoomStateException e)
    {
        return Results.BadRequest(e.Message);
    }
    catch (SpectatorCannotVoteException)
    {
        return Results.BadRequest("Spectators cannot vote");
    }
    catch (ParticipantNotFoundException)
    {
        return Results.BadRequest("Not a participant");
    }
    catch (InvalidVoteValueException e)
    {
        return Results.BadRequest(e.Message);
    }
});

// ─── DELETE /estimation/rooms/{code}/votes/me ────────────────────────────

app.MapDelete("/estimation/rooms/{code}/votes/me", async (
    string code,
    EstimationRoomService svc,
    WebSocketRoomManager wsManager,
    HttpContext ctx) =>
{
    var (participantId, _, _, _, _) = ResolveIdentity(ctx);

    try
    {
        var room = await svc.ClearVoteAsync(code, participantId);
        await BroadcastRoomUpdate(wsManager, room, code, baseUrl);
        return Results.NoContent();
    }
    catch (RoomNotFoundException)
    {
        return Results.NotFound("Room not found");
    }
    catch (InvalidRoomStateException e)
    {
        return Results.BadRequest(e.Message);
    }
});

// ─── POST /estimation/rooms/{code}/reveal ────────────────────────────────

app.MapPost("/estimation/rooms/{code}/reveal", async (
    string code,
    EstimationRoomService svc,
    WebSocketRoomManager wsManager,
    HttpContext ctx) =>
{
    var userId = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier);
    if (userId is null) return Results.Unauthorized();

    try
    {
        var room = await svc.RevealVotesAsync(code, userId);
        var response = RoomResponseMapper.ToResponse(room, userId, baseUrl);

        await BroadcastRoomUpdate(wsManager, room, code, baseUrl);
        return Results.Ok(response);
    }
    catch (RoomNotFoundException)
    {
        return Results.NotFound("Room not found");
    }
    catch (NotModeratorException)
    {
        return Results.Forbid();
    }
    catch (InvalidRoomStateException e)
    {
        return Results.BadRequest(e.Message);
    }
}).RequireAuthorization();

// ─── POST /estimation/rooms/{code}/reset ─────────────────────────────────

app.MapPost("/estimation/rooms/{code}/reset", async (
    string code,
    EstimationRoomService svc,
    WebSocketRoomManager wsManager,
    HttpContext ctx) =>
{
    var userId = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier);
    if (userId is null) return Results.Unauthorized();

    try
    {
        var room = await svc.ResetRoundAsync(code, userId);
        var response = RoomResponseMapper.ToResponse(room, userId, baseUrl);

        await BroadcastRoomUpdate(wsManager, room, code, baseUrl);
        return Results.Ok(response);
    }
    catch (RoomNotFoundException)
    {
        return Results.NotFound("Room not found");
    }
    catch (NotModeratorException)
    {
        return Results.Forbid();
    }
    catch (RoomArchivedException)
    {
        return Results.BadRequest("Room is archived");
    }
}).RequireAuthorization();

// ─── POST /estimation/rooms/{code}/archive ───────────────────────────────

app.MapPost("/estimation/rooms/{code}/archive", async (
    string code,
    EstimationRoomService svc,
    WebSocketRoomManager wsManager,
    HttpContext ctx) =>
{
    var userId = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier);
    if (userId is null) return Results.Unauthorized();

    try
    {
        var room = await svc.ArchiveRoomAsync(code, userId);
        var response = RoomResponseMapper.ToResponse(room, userId, baseUrl);

        await BroadcastRoomUpdate(wsManager, room, code, baseUrl);
        return Results.Ok(response);
    }
    catch (RoomNotFoundException)
    {
        return Results.NotFound("Room not found");
    }
    catch (NotModeratorException)
    {
        return Results.Forbid();
    }
    catch (InvalidRoomStateException e)
    {
        return Results.BadRequest(e.Message);
    }
}).RequireAuthorization();

// ─── GET /estimation/decks ───────────────────────────────────────────────

app.MapGet("/estimation/decks", () =>
{
    var decks = Decks.All.Values.Select(d => new DeckDefinitionResponse(d.Id, d.Name, d.Values));
    return Results.Ok(decks);
});

// ─── WebSocket: /estimation/rooms/{code}/ws ──────────────────────────────

app.Map("/estimation/rooms/{code}/ws", async (
    string code,
    HttpContext ctx,
    EstimationRoomService svc,
    WebSocketRoomManager wsManager) =>
{
    if (!ctx.WebSockets.IsWebSocketRequest)
    {
        ctx.Response.StatusCode = 400;
        return;
    }

    var (participantId, _, _, _, _) = ResolveIdentity(ctx);

    var room = await svc.GetRoomByCodeAsync(code);
    if (room is null)
    {
        ctx.Response.StatusCode = 404;
        return;
    }

    using var ws = await ctx.WebSockets.AcceptWebSocketAsync();
    wsManager.AddConnection(code, participantId, ws);

    // Send initial room state
    var initialResponse = RoomResponseMapper.ToResponse(room, participantId, baseUrl);
    var initialJson = JsonSerializer.Serialize(initialResponse, jsonOptions);
    await ws.SendAsync(Encoding.UTF8.GetBytes(initialJson), WebSocketMessageType.Text, true, CancellationToken.None);

    // Keep connection alive and read incoming messages (heartbeats)
    var buffer = new byte[1024];
    try
    {
        while (ws.State == WebSocketState.Open)
        {
            var result = await ws.ReceiveAsync(buffer, CancellationToken.None);
            if (result.MessageType == WebSocketMessageType.Close)
                break;
        }
    }
    catch (WebSocketException)
    {
        // Client disconnected
    }
    finally
    {
        wsManager.RemoveConnection(code, participantId);
        if (ws.State is WebSocketState.Open or WebSocketState.CloseReceived)
        {
            try
            {
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed", CancellationToken.None);
            }
            catch
            {
                /* ignore */
            }
        }
    }
});

app.MapDefaultEndpoints();

app.Run();

// ─── Helper: broadcast room update to all WebSocket clients ─────────────

static async Task BroadcastRoomUpdate(
    WebSocketRoomManager wsManager, EstimationRoomView room, string code, string baseUrl)
{
    await wsManager.BroadcastRoomUpdateAsync(code, participantId =>
        RoomResponseMapper.ToResponse(room, participantId, baseUrl));
}