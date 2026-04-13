using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Overflow.Common.Options;
using Overflow.Contracts;
using Overflow.ProfileService.Data;
using Overflow.ProfileService.Services;

namespace Overflow.ProfileService.MessageHandlers;

public class UserDeletedHandler(
    ProfileDbContext db,
    KeycloakAdminClient keycloakAdmin,
    IOptions<KeycloakOptions> keycloakOptions,
    ILogger<UserDeletedHandler> logger)
{
    private readonly KeycloakOptions _kc = keycloakOptions.Value;

    private string TokenUrl => $"{_kc.Url}/realms/{_kc.Realm}/protocol/openid-connect/token";
    private string AdminBaseUrl => $"{_kc.Url}/admin/realms/{_kc.Realm}";

    public async Task Handle(UserDeleted message)
    {
        var userId = message.UserId;
        logger.LogInformation("Handling UserDeleted for {UserId}", userId);

        // 1. Delete from Keycloak (source of auth)
        if (!string.IsNullOrEmpty(_kc.AdminClientId) && !string.IsNullOrEmpty(_kc.AdminClientSecret))
        {
            try
            {
                await keycloakAdmin.AuthenticateAsync(TokenUrl, _kc.AdminClientId, _kc.AdminClientSecret);
                await keycloakAdmin.DeleteUserAsync(AdminBaseUrl, userId);
                logger.LogInformation("Deleted Keycloak user {UserId}", userId);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to delete Keycloak user {UserId} — may not exist", userId);
            }
        }
        else
        {
            logger.LogWarning("Keycloak admin credentials not configured — skipping Keycloak deletion for {UserId}",
                userId);
        }

        // 2. Delete from ProfileService DB
        var deleted = await db.UserProfiles
            .Where(p => p.Id == userId)
            .ExecuteDeleteAsync();

        logger.LogInformation("Deleted {Count} profile record(s) for user {UserId}", deleted, userId);
    }
}

