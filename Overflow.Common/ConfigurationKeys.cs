namespace Overflow.Common;

/// <summary>
/// Well-known configuration keys used across services.
/// Eliminates magic strings and provides a single source of truth for <c>builder.Configuration["..."]</c> lookups.
/// </summary>
public static class ConfigurationKeys
{
    // ── Infisical ────────────────────────────────────────────────────────
    public const string InfisicalClientId = "INFISICAL_CLIENT_ID";
    public const string InfisicalClientSecret = "INFISICAL_CLIENT_SECRET";
    public const string InfisicalProjectId = "INFISICAL_PROJECT_ID";

    // ── OpenTelemetry ────────────────────────────────────────────────────
    public const string OtelServiceName = "OTEL_SERVICE_NAME";

    // ── Service URLs ─────────────────────────────────────────────────────
    public const string ProfileServiceUrl = "PROFILE_SERVICE_URL";

    // ── NotificationService ──────────────────────────────────────────────
    public const string NotificationInternalApiKey = "Notification:InternalApiKey";

    // ── Typesense ────────────────────────────────────────────────────────
    public const string TypesenseApiKey = "TypesenseOptions:ApiKey";
}