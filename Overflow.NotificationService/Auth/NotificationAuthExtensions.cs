using Microsoft.AspNetCore.Authorization;
using Overflow.Common;

namespace Overflow.NotificationService.Auth;

/// <summary>
/// Registers API key authentication for server-to-server calls to NotificationService.
///
/// The webapp's forgot-password route has no user session, so it cannot send a Keycloak JWT.
/// Instead it sends an <c>X-Api-Key</c> header with a shared secret (<c>NOTIFICATION_INTERNAL_API_KEY</c>
/// in config / Infisical).
///
/// After calling this method, the default <c>[Authorize]</c> policy accepts
/// <b>either</b> a valid Keycloak JWT (<c>Bearer</c>) <b>or</b> a valid API key.
/// </summary>
public static class NotificationAuthExtensions
{
    /// <summary>
    /// Adds API-key authentication and configures the default authorization policy
    /// to accept either Keycloak JWT or the <c>X-Api-Key</c> header.
    /// </summary>
    public static WebApplicationBuilder AddNotificationApiKeyAuth(this WebApplicationBuilder builder)
    {
        builder.Services
            .AddAuthentication()
            .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
                ApiKeyAuthenticationHandler.SchemeName,
                options => { options.ApiKey = builder.Configuration[ConfigurationKeys.NotificationInternalApiKey]; });

        builder.Services.AddAuthorization(options =>
        {
            options.DefaultPolicy = new AuthorizationPolicyBuilder(
                    ApiKeyAuthenticationHandler.SchemeName, "Bearer")
                .RequireAuthenticatedUser()
                .Build();
        });

        return builder;
    }
}