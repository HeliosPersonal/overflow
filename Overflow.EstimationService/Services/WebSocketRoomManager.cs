using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Overflow.EstimationService.Services;

/// <summary>
/// Manages raw WebSocket connections grouped by room code.
/// When room state changes, the service broadcasts the updated room snapshot
/// to all connected clients in that room.
/// </summary>
public class WebSocketRoomManager
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// room code → set of (participantId, WebSocket)
    /// </summary>
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, WebSocket>> _rooms = new();

    private readonly ILogger<WebSocketRoomManager> _logger;

    public WebSocketRoomManager(ILogger<WebSocketRoomManager> logger)
    {
        _logger = logger;
    }

    public void AddConnection(string roomCode, string participantId, WebSocket ws)
    {
        var room = _rooms.GetOrAdd(roomCode, _ => new ConcurrentDictionary<string, WebSocket>());

        // Close old connection for same participant if exists
        if (room.TryGetValue(participantId, out var oldWs) && oldWs != ws)
        {
            _ = TryCloseAsync(oldWs);
        }

        room[participantId] = ws;
        _logger.LogDebug("WebSocket connected: {ParticipantId} in room {Code}", participantId, roomCode);
    }

    public void RemoveConnection(string roomCode, string participantId)
    {
        if (_rooms.TryGetValue(roomCode, out var room))
        {
            room.TryRemove(participantId, out _);
            _logger.LogDebug("WebSocket disconnected: {ParticipantId} from room {Code}", participantId, roomCode);

            if (room.IsEmpty)
            {
                _rooms.TryRemove(roomCode, out _);
            }
        }
    }

    /// <summary>
    /// Broadcasts a JSON message to all connected clients in a room.
    /// Each client receives a viewer-scoped snapshot (built by the caller).
    /// </summary>
    public async Task BroadcastRoomUpdateAsync(string roomCode, Func<string, object?> getPayloadForParticipant)
    {
        if (!_rooms.TryGetValue(roomCode, out var room)) return;

        var tasks = new List<Task>();

        foreach (var (participantId, ws) in room)
        {
            if (ws.State != WebSocketState.Open)
            {
                room.TryRemove(participantId, out _);
                continue;
            }

            var payload = getPayloadForParticipant(participantId);
            if (payload is null) continue;

            var json = JsonSerializer.Serialize(payload, JsonOptions);
            var bytes = Encoding.UTF8.GetBytes(json);

            tasks.Add(SendAsync(ws, bytes, participantId, roomCode));
        }

        await Task.WhenAll(tasks);
    }

    private async Task SendAsync(WebSocket ws, byte[] data, string participantId, string roomCode)
    {
        try
        {
            if (ws.State == WebSocketState.Open)
            {
                await ws.SendAsync(data, WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send WebSocket message to {ParticipantId} in room {Code}",
                participantId, roomCode);
        }
    }

    private static async Task TryCloseAsync(WebSocket ws)
    {
        try
        {
            if (ws.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Replaced", CancellationToken.None);
            }
        }
        catch
        {
            // ignore close errors on stale sockets
        }
    }
}