using Overflow.DataSeederService.Keycloak;
using Overflow.DataSeederService.Models;

namespace Overflow.DataSeederService.Services;

/// <summary>Singleton pool of seeder accounts. Lazily loaded and auto-refreshed every 30 min. Thread-safe.</summary>
public class SeederUserPool(
    UserSyncService syncService,
    SeederUserService seederUserService)
{
    private static readonly TimeSpan SyncInterval = TimeSpan.FromMinutes(30);
    private readonly SemaphoreSlim _lock = new(1, 1);
    private List<SeederUser> _pool = [];
    private DateTime _syncedAt = DateTime.MinValue;

    /// <summary>Returns the pool, triggering a sync if empty or stale.</summary>
    public async Task<List<SeederUser>> GetAsync(CancellationToken ct = default)
    {
        if (_pool.Count > 0 && DateTime.UtcNow - _syncedAt < SyncInterval)
        {
            return _pool;
        }

        await _lock.WaitAsync(ct);
        try
        {
            if (_pool.Count > 0 && DateTime.UtcNow - _syncedAt < SyncInterval)
            {
                return _pool;
            }

            _pool = await syncService.SyncPoolAsync(ct);
            _syncedAt = DateTime.UtcNow;
        }
        finally
        {
            _lock.Release();
        }

        return _pool;
    }

    /// <summary>Fetches a fresh token for the given user and updates their cached token.</summary>
    public async Task<string?> RefreshTokenAsync(SeederUser user, CancellationToken ct = default)
    {
        var token = await seederUserService.GetFreshTokenAsync(user.KeycloakUserId, ct);
        if (token != null)
        {
            user.Token = token;
        }

        return token;
    }
}