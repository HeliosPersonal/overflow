using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Overflow.Common;
using Overflow.EstimationService.Clients;
using Overflow.EstimationService.Data;
using Overflow.EstimationService.Mapping;
using Overflow.EstimationService.Models;

namespace Overflow.EstimationService.Services;

/// <summary>
/// Tracks active WebSocket connections per room and broadcasts viewer-scoped
/// <see cref="DTOs.RoomResponse"/> snapshots to all connected participants.
/// Replaces the old <c>RedisBroadcaster</c> + <c>RedisSubscriptionManager</c> + <c>WebSocketRoomManager</c>.
/// </summary>
public class WebSocketBroadcaster(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<WebSocketBroadcaster> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Wraps a <see cref="WebSocket"/> with a per-connection send lock so that
    /// concurrent broadcast tasks are serialized without blocking unrelated connections.
    /// </summary>
    public sealed class Connection(WebSocket socket)
    {
        public WebSocket Socket { get; } = socket;
        private readonly SemaphoreSlim _sendLock = new(1, 1);

        public async Task SendAsync(string json, CancellationToken ct = default)
        {
            await _sendLock.WaitAsync(ct);
            try
            {
                if (Socket.State == WebSocketState.Open)
                {
                    var bytes = Encoding.UTF8.GetBytes(json);
                    await Socket.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, ct);
                }
            }
            finally
            {
                _sendLock.Release();
            }
        }
    }

    private readonly ConcurrentDictionary<(Guid RoomId, string ParticipantId), Connection> _connections = new();

    private string BaseUrl => configuration[ConfigurationKeys.AppBaseUrl] ?? "http://localhost:3000";

    public Connection AddConnection(Guid roomId, string participantId, WebSocket socket)
    {
        var key = (roomId, participantId);

        if (_connections.TryGetValue(key, out var old) && old.Socket != socket)
            _ = TryCloseAsync(old.Socket);

        var connection = new Connection(socket);
        _connections[key] = connection;
        logger.LogDebug("WebSocket connected: {ParticipantId} in room {RoomId}", participantId, roomId);
        return connection;
    }

    public void RemoveConnection(Guid roomId, string participantId)
    {
        if (_connections.TryRemove((roomId, participantId), out _))
            logger.LogDebug("WebSocket disconnected: {ParticipantId} from room {RoomId}", participantId, roomId);
    }

    /// <summary>
    /// Loads the room from DB and broadcasts a viewer-scoped snapshot to every
    /// connected participant in the room. Called after any mutation.
    /// Resolves avatar URLs from ProfileService (via FusionCache) at broadcast time.
    /// </summary>
    public async Task BroadcastRoomUpdateAsync(Guid roomId)
    {
        try
        {
            // Load fresh room from DB in a new scope
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<EstimationDbContext>();
            var room = await db.Rooms
                .Include(r => r.Participants)
                .Include(r => r.Votes)
                .Include(r => r.RoundHistory)
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == roomId);

            if (room is null) return;

            // Resolve avatars for all authenticated participants
            var profileClient = scope.ServiceProvider.GetRequiredService<ProfileServiceClient>();
            var avatarLookup = await ResolveAvatarsAsync(profileClient, room);

            await BroadcastRoomAsync(room, avatarLookup);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to broadcast room update for {RoomId}", roomId);
        }
    }

    /// <summary>
    /// Broadcasts a pre-loaded room to all connected participants.
    /// Useful when the caller already has the room loaded.
    /// </summary>
    public async Task BroadcastRoomAsync(EstimationRoom room,
        IReadOnlyDictionary<string, string?>? avatarLookup = null)
    {
        avatarLookup ??= new Dictionary<string, string?>();
        var tasks = new List<Task>();
        var baseUrl = BaseUrl;

        foreach (var ((roomId, participantId), connection) in _connections)
        {
            if (roomId != room.Id) continue;

            if (connection.Socket.State != WebSocketState.Open)
            {
                _connections.TryRemove((roomId, participantId), out _);
                continue;
            }

            var response = RoomResponseMapper.ToResponse(room, participantId, baseUrl, avatarLookup);
            var json = JsonSerializer.Serialize(response, JsonOptions);
            tasks.Add(SendSafeAsync(connection, json, participantId, roomId));
        }

        if (tasks.Count > 0)
            await Task.WhenAll(tasks);
    }

    private async Task SendSafeAsync(Connection connection, string json, string participantId, Guid roomId)
    {
        try
        {
            await connection.SendAsync(json);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send WebSocket message to {ParticipantId} in room {RoomId}",
                participantId, roomId);
        }
    }

    private static async Task TryCloseAsync(WebSocket socket)
    {
        try
        {
            if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Replaced", CancellationToken.None);
        }
        catch
        {
            // Ignore errors on stale sockets
        }
    }

    private static async Task<Dictionary<string, string?>> ResolveAvatarsAsync(
        ProfileServiceClient profileClient, EstimationRoom room)
    {
        var userIds = room.Participants
            .Where(p => p.UserId is not null)
            .Select(p => p.UserId!)
            .Distinct()
            .ToList();

        var result = new Dictionary<string, string?>();
        await Task.WhenAll(userIds.Select(async userId =>
        {
            var profile = await profileClient.GetProfileDataAsync(userId);
            lock (result)
            {
                result[userId] = profile?.AvatarUrl;
            }
        }));

        return result;
    }
}