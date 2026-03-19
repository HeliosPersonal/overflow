using Bogus;
using Microsoft.Extensions.Options;
using Overflow.DataSeederService.Clients;
using Overflow.DataSeederService.Keycloak;
using Overflow.DataSeederService.Models;

namespace Overflow.DataSeederService.Services;

/// <summary>Syncs the seeder user pool with Keycloak and the Profile Service. Real accounts are never touched.</summary>
public class UserSyncService(
    IProfileApiClient profileApi,
    IOptions<SeederOptions> options,
    SeederUserService seederUserService,
    KeycloakAdminService keycloakAdmin,
    ILogger<UserSyncService> logger)
{
    private readonly Faker _faker = new();
    private readonly SeederOptions _options = options.Value;

    public async Task<List<SeederUser>> SyncPoolAsync(CancellationToken ct = default)
    {
        var pool = new List<SeederUser>();

        logger.LogInformation("Syncing seeder pool — looking up existing '{Prefix}*' accounts in Keycloak",
            _options.SeederUsernamePrefix);

        var existing = await keycloakAdmin.SearchUsersByPrefixAsync(
            _options.SeederUsernamePrefix, _options.MaxSeederUsers, ct);

        logger.LogInformation("Found {Count} existing seeder account(s) in Keycloak", existing.Count);

        foreach (var (userId, email) in existing)
        {
            if (pool.Count >= _options.MaxSeederUsers)
            {
                break;
            }

            var (_, token) = await seederUserService.AuthenticateExistingUserAsync(userId, email, ct);

            if (token != null)
            {
                await EnsureProfileExistsAsync(token, ct);
                pool.Add(new SeederUser(userId, email, token));
            }
            else
            {
                logger.LogWarning("Skipping {Email} — could not authenticate", email);
            }

            await Task.Delay(Random.Shared.Next(50, 200), ct);
        }

        logger.LogInformation("Authenticated {Loaded}/{Found} existing seeder account(s)", pool.Count, existing.Count);

        var toCreate = _options.MaxSeederUsers - pool.Count;
        if (toCreate > 0)
        {
            logger.LogInformation("Creating {Count} new seeder account(s) to reach pool size of {Max}",
                toCreate, _options.MaxSeederUsers);

            for (var i = 0; i < toCreate; i++)
            {
                var user = await CreateNewUserAsync(ct);
                if (user != null)
                {
                    pool.Add(user);
                }

                await Task.Delay(Random.Shared.Next(100, 400), ct);
            }
        }

        logger.LogInformation("Seeder pool ready — {Count}/{Max} account(s)", pool.Count, _options.MaxSeederUsers);
        return pool;
    }

    private async Task<SeederUser?> CreateNewUserAsync(CancellationToken ct)
    {
        var displayName = _faker.Name.FullName();
        var (userId, token) = await seederUserService.CreateUserAsync(displayName, ct);

        if (userId == null || token == null)
        {
            logger.LogWarning("Failed to create new seeder user '{DisplayName}'", displayName);
            return null;
        }

        await EnsureProfileExistsAsync(token, ct);
        logger.LogInformation("Created seeder account '{DisplayName}' ({UserId})", displayName, userId);
        return new SeederUser(userId, displayName, token);
    }

    /// <summary>Calls GET /profiles/me to trigger profile auto-creation. No-op if profile already exists.</summary>
    private async Task EnsureProfileExistsAsync(string token, CancellationToken ct)
    {
        try
        {
            await profileApi.GetMyProfileAsync($"Bearer {token}", ct);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Profile auto-creation request failed (non-critical, will retry next sync)");
        }
    }
}