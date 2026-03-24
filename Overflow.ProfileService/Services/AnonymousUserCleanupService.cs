using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Overflow.Common.Options;
using Overflow.ProfileService.Data;
using Overflow.ProfileService.Options;

namespace Overflow.ProfileService.Services;

/// <summary>
/// Periodically deletes anonymous (guest) users who never completed registration.
/// Anonymous users are identified by their Keycloak email ending with
/// <see cref="KeycloakAdminClient.AnonymousEmailDomain"/>.
///
/// Cleanup removes:
/// 1. The Keycloak user (so credentials and sessions are gone)
/// 2. The ProfileService database record (UserProfile)
///
/// Configuration via <c>AnonymousCleanup</c> section in appsettings.
/// </summary>
public class AnonymousUserCleanupService(
    IServiceScopeFactory scopeFactory,
    KeycloakAdminClient keycloakAdmin,
    IOptions<KeycloakOptions> keycloakOptions,
    IOptions<AnonymousCleanupOptions> cleanupOptions,
    ILogger<AnonymousUserCleanupService> logger) : BackgroundService
{
    private readonly KeycloakOptions _kc = keycloakOptions.Value;
    private readonly AnonymousCleanupOptions _cleanup = cleanupOptions.Value;

    private string TokenUrl => $"{_kc.Url}/realms/{_kc.Realm}/protocol/openid-connect/token";
    private string AdminBaseUrl => $"{_kc.Url}/admin/realms/{_kc.Realm}";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Skip if admin credentials are not configured (e.g. in integration tests)
        if (string.IsNullOrEmpty(_kc.AdminClientId) || string.IsNullOrEmpty(_kc.AdminClientSecret))
        {
            logger.LogWarning(
                "Anonymous user cleanup disabled — Keycloak admin credentials not configured");
            return;
        }

        logger.LogInformation(
            "Anonymous user cleanup service started. GuestAccountMaxAge={MaxAgeDays}d, Interval={IntervalHours}h",
            _cleanup.GuestAccountMaxAgeDays, _cleanup.IntervalHours);

        // Initial delay to let the app finish starting up
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var deleted = await CleanupAsync(stoppingToken);
                if (deleted > 0)
                    logger.LogInformation(
                        "Deleted {Count} anonymous user(s) older than {Days} days",
                        deleted, _cleanup.GuestAccountMaxAgeDays);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Error during anonymous user cleanup");
            }

            await Task.Delay(TimeSpan.FromHours(_cleanup.IntervalHours), stoppingToken);
        }
    }

    private async Task<int> CleanupAsync(CancellationToken ct)
    {
        // 1. Authenticate with Keycloak Admin API
        await keycloakAdmin.AuthenticateAsync(TokenUrl, _kc.AdminClientId!, _kc.AdminClientSecret!, ct);

        // 2. Find all anonymous users in Keycloak
        var anonymousUsers = await keycloakAdmin.FindAnonymousUsersAsync(AdminBaseUrl, ct);

        if (anonymousUsers.Count == 0)
        {
            logger.LogDebug("No anonymous users found in Keycloak");
            return 0;
        }

        // 3. Filter to only those older than the retention period
        var cutoff = DateTime.UtcNow.AddDays(-_cleanup.GuestAccountMaxAgeDays);
        var staleUsers = anonymousUsers
            .Where(u => u.CreatedAtUtc < cutoff)
            .ToList();

        if (staleUsers.Count == 0)
        {
            logger.LogDebug(
                "Found {Total} anonymous user(s) but none older than {Days} days",
                anonymousUsers.Count, _cleanup.GuestAccountMaxAgeDays);
            return 0;
        }

        logger.LogInformation(
            "Found {Stale} stale anonymous user(s) out of {Total} total (cutoff: {Cutoff:u})",
            staleUsers.Count, anonymousUsers.Count, cutoff);

        var deleted = 0;

        foreach (var user in staleUsers)
        {
            try
            {
                logger.LogInformation(
                    "Deleting anonymous user {UserId} ({Email}) — created {CreatedAt:u}",
                    user.Id, user.Email, user.CreatedAtUtc);

                // Delete from Keycloak first (source of auth)
                await keycloakAdmin.DeleteUserAsync(AdminBaseUrl, user.Id, ct);

                // Delete the profile from ProfileService DB
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<ProfileDbContext>();
                await db.UserProfiles
                    .Where(p => p.Id == user.Id)
                    .ExecuteDeleteAsync(ct);

                deleted++;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "Failed to delete anonymous user {UserId} ({Email}) — will retry next cycle",
                    user.Id, user.Email);
            }
        }

        return deleted;
    }
}