using System.Net.WebSockets;
using System.Text.Json;
using System.Text.Json.Serialization;
using Overflow.EstimationService.Auth;
using Overflow.EstimationService.Mapping;
using Overflow.EstimationService.Services;

namespace Overflow.EstimationService.Extensions;

/// <summary>
/// Registers the read-only WebSocket endpoint for real-time room updates.
/// Clients connect and receive viewer-scoped room snapshots. All mutations go through HTTP.
/// On disconnect, the participant is auto-removed from the room.
/// </summary>
public static class WebSocketEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static WebApplication MapEstimationWebSocket(this WebApplication app)
    {
        app.Map("/estimation/rooms/{roomId:guid}/ws", async (
            Guid roomId,
            HttpContext ctx,
            EstimationRoomService svc,
            WebSocketBroadcaster broadcaster,
            IdentityResolver identityResolver,
            IServiceScopeFactory scopeFactory,
            IConfiguration configuration,
            ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("EstimationWebSocket");

            if (!ctx.WebSockets.IsWebSocketRequest)
            {
                ctx.Response.StatusCode = 400;
                return;
            }


            var identity = await identityResolver.ResolveAsync(ctx);

            var room = await svc.GetRoomByIdAsync(roomId);
            if (room is null)
            {
                ctx.Response.StatusCode = 404;
                return;
            }

            var baseUrl = configuration["APP_BASE_URL"] ?? "http://localhost:3000";
            var socket = await ctx.WebSockets.AcceptWebSocketAsync();

            var connection = broadcaster.AddConnection(roomId, identity.ParticipantId, socket);

            // Send initial room snapshot
            var initialResponse = RoomResponseMapper.ToResponse(room, identity.ParticipantId, baseUrl);
            var initialJson = JsonSerializer.Serialize(initialResponse, JsonOptions);
            try
            {
                await connection.SendAsync(initialJson);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to send initial state to {ParticipantId} in room {RoomId}",
                    identity.ParticipantId, roomId);
                broadcaster.RemoveConnection(roomId, identity.ParticipantId);
                return;
            }

            // ── Read-only receive loop (just keeps the connection alive) ──────────
            var receiveBuffer = new byte[4096];
            try
            {
                while (socket.State == WebSocketState.Open)
                {
                    var result = await socket.ReceiveAsync(receiveBuffer, CancellationToken.None);
                    if (result.MessageType == WebSocketMessageType.Close)
                        break;
                    // All client messages are ignored — mutations go through HTTP
                }
            }
            catch (WebSocketException)
            {
                // Client disconnected abruptly — clean up in finally
            }
            finally
            {
                broadcaster.RemoveConnection(roomId, identity.ParticipantId);

                // Disconnect = mark absent: set IsPresent = false on the participant
                try
                {
                    using var scope = scopeFactory.CreateScope();
                    var leaveService = scope.ServiceProvider.GetRequiredService<EstimationRoomService>();
                    var leaveResult = await leaveService.LeaveRoomAsync(roomId, identity.ParticipantId);
                    if (leaveResult.IsSuccess)
                        logger.LogInformation(
                            "Participant {ParticipantId} marked absent in room {RoomId} on WS disconnect",
                            identity.ParticipantId, roomId);
                    else
                        logger.LogWarning(
                            "Failed to auto-leave participant {ParticipantId} from room {RoomId}: {Error}",
                            identity.ParticipantId, roomId, leaveResult.Error.Message);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to auto-leave participant {ParticipantId} from room {RoomId}",
                        identity.ParticipantId, roomId);
                }

                if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
                {
                    try
                    {
                        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed",
                            CancellationToken.None);
                    }
                    catch
                    {
                        // Ignore errors during shutdown close handshake
                    }
                }
            }
        });

        return app;
    }
}