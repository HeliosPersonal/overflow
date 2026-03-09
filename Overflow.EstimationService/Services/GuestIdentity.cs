namespace Overflow.EstimationService.Services;

/// <summary>
/// Manages server-issued guest identities via cookies. Provides a stable guest ID
/// that survives browser refresh.
/// </summary>
public static class GuestIdentity
{
    public const string CookieName = "overflow_guest_id";
    private const int CookieExpirationDays = 30;

    public static string? GetGuestId(HttpContext ctx)
    {
        return ctx.Request.Cookies[CookieName];
    }

    public static string EnsureGuestId(HttpContext ctx)
    {
        var existing = GetGuestId(ctx);
        if (!string.IsNullOrEmpty(existing)) return existing;

        var guestId = $"guest_{Guid.NewGuid():N}";
        SetGuestCookie(ctx, guestId);
        return guestId;
    }

    public static void SetGuestCookie(HttpContext ctx, string guestId)
    {
        ctx.Response.Cookies.Append(CookieName, guestId, new CookieOptions
        {
            HttpOnly = true,
            Secure = false, // Allow HTTP for local dev; production will use HTTPS via reverse proxy
            SameSite = SameSiteMode.Lax,
            MaxAge = TimeSpan.FromDays(CookieExpirationDays),
            Path = "/"
        });
    }
}