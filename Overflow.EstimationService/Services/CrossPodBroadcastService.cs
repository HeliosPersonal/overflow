using StackExchange.Redis;

namespace Overflow.EstimationService.Services;

/// <summary>
/// Uses Redis pub/sub to notify all pods when a room needs a WebSocket broadcast.
/// This solves the multi-pod problem: when pod A mutates a room, pod B (which may hold
/// WebSocket connections for that room) also receives the notification and broadcasts
/// to its local connections.
///
/// Flow:
///   Mutation on Pod A → InvalidateCache → PublishRoomUpdate(roomId)
///   Redis pub/sub → All pods receive notification
///   Each pod → WebSocketBroadcaster.BroadcastRoomUpdateAsync(roomId) for LOCAL connections only
///
/// The channel name is prefixed with the environment name (e.g. "staging:", "production:")
/// so that multiple environments sharing the same Redis instance are fully isolated.
/// </summary>
public class CrossPodBroadcastService : IHostedService, IAsyncDisposable
{
    private readonly string _channel;

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

        _logger.LogInformation("Cross-pod broadcast subscriber started on channel '{Channel}'", _channel);
    }

    private async Task HandleBroadcastAsync(string? message)
    {
        try
        {
            if (Guid.TryParse(message, out var roomId))
            {
                _logger.LogDebug("Received cross-pod broadcast for room {RoomId}", roomId);
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
    /// Publishes a room update notification to all pods.
    /// </summary>
    public async Task PublishRoomUpdateAsync(Guid roomId)
    {
        try
        {
            var subscriber = _redis.GetSubscriber();
            await subscriber.PublishAsync(RedisChannel.Literal(_channel), roomId.ToString());
            _logger.LogDebug("Published cross-pod broadcast for room {RoomId}", roomId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish cross-pod broadcast for room {RoomId} — " +
                                   "falling back to local-only broadcast", roomId);
            // Fallback: at least broadcast locally
            await _broadcaster.BroadcastRoomUpdateAsync(roomId);
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