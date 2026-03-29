using System.Security.Claims;

namespace Overflow.Common.CommonExtensions;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Returns the authenticated user's ID (Keycloak sub claim), or <c>null</c> if absent.
    /// </summary>
    public static string? GetUserId(this ClaimsPrincipal user)
        => user.FindFirstValue(ClaimTypes.NameIdentifier);

    /// <summary>
    /// Returns the authenticated user's ID, throwing if the claim is missing.
    /// Use from <c>[Authorize]</c> endpoints where the user is guaranteed to be authenticated.
    /// </summary>
    public static string GetRequiredUserId(this ClaimsPrincipal user)
        => user.FindFirstValue(ClaimTypes.NameIdentifier)
           ?? throw new InvalidOperationException("User ID claim is missing from the authenticated principal.");
}