using StackExchange.Redis;

namespace Overflow.EstimationService.Services;

/// <summary>
/// Uses Redis pub/sub to notify all pods when a room needs a WebSocket broadcast.
/// This solves the multi-pod problem: when pod A mutates a room, pod B (which may hold
/// WebSocket connections for that room) also receives the notification and broadcasts
/// to its local connections.
///
/// Flow:
///   Mutation on Pod A → PublishRoomUpdate(roomId)
///     1. Local broadcast immediately (Pod A sockets)
///     2. Redis pub/sub → Other pods receive notification → broadcast to their sockets
///
/// Messages are tagged with the pod's unique instance ID so the sender can ignore
/// its own echo from Redis (it already handled the local broadcast directly).
///
/// The channel name is prefixed with the environment name (e.g. "staging:", "production:")
/// so that multiple environments sharing the same Redis instance are fully isolated.
/// </summary>
public class CrossPodBroadcastService : IHostedService, IAsyncDisposable
{
    private readonly string _channel;
    private readonly string _instanceId = Guid.NewGuid().ToString("N")[..8];

    private readonly IConnectionMultiplexer _redis;
    private readonly WebSocketBroadcaster _broadcaster;
    private readonly ILogger<CrossPodBroadcastService> _logger;
    private ISubscriber? _subscriber;

    public CrossPodBroadcastService(
        IConnectionMultiplexer redis,
        WebSocketBroadcaster broadcaster,
        IHostEnvironment environment,
        ILogger<CrossPodBroadcastService> logger)
    {
        _redis = redis;
        _broadcaster = broadcaster;
        _logger = logger;
        _channel = $"{environment.EnvironmentName.ToLowerInvariant()}:estimation:room-updates";
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _subscriber = _redis.GetSubscriber();
        var queue = await _subscriber.SubscribeAsync(RedisChannel.Literal(_channel));
        queue.OnMessage(channelMessage => { _ = HandleBroadcastAsync(channelMessage.Message); });

        _logger.LogInformation(
            "Cross-pod broadcast subscriber started on channel '{Channel}' (instance {InstanceId})",
            _channel, _instanceId);
    }

    private async Task HandleBroadcastAsync(string? message)
    {
        try
        {
            if (message is null) return;

            // Message format: "instanceId:roomId" — skip our own messages
            var separatorIndex = message.IndexOf(':');
            if (separatorIndex < 0) return;

            var senderId = message[..separatorIndex];
            var roomIdStr = message[(separatorIndex + 1)..];

            if (senderId == _instanceId)
            {
                _logger.LogDebug("Skipping own broadcast echo for room {RoomId}", roomIdStr);
                return;
            }

            if (Guid.TryParse(roomIdStr, out var roomId))
            {
                _logger.LogDebug("Received cross-pod broadcast for room {RoomId} from {SenderId}", roomId, senderId);
                await _broadcaster.BroadcastRoomUpdateAsync(roomId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling cross-pod broadcast for message: {Message}", message);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_subscriber is not null)
        {
            await _subscriber.UnsubscribeAsync(RedisChannel.Literal(_channel));
            _logger.LogInformation("Cross-pod broadcast subscriber stopped");
        }
    }

    /// <summary>
    /// Broadcasts a room update to local WebSocket connections immediately,
    /// then publishes to Redis so other pods do the same.
    /// </summary>
    public virtual async Task PublishRoomUpdateAsync(Guid roomId)
    {
        // 1. Broadcast locally first — fastest path for connections on this pod
        try
        {
            await _broadcaster.BroadcastRoomUpdateAsync(roomId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Local broadcast failed for room {RoomId}", roomId);
        }

        // 2. Notify other pods via Redis pub/sub (tagged with instance ID to skip echo)
        try
        {
            var subscriber = _redis.GetSubscriber();
            var payload = $"{_instanceId}:{roomId}";
            await subscriber.PublishAsync(RedisChannel.Literal(_channel), payload);
            _logger.LogDebug("Published cross-pod broadcast for room {RoomId}", roomId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish cross-pod broadcast for room {RoomId}", roomId);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_subscriber is not null)
        {
            await _subscriber.UnsubscribeAllAsync();
        }
    }
}