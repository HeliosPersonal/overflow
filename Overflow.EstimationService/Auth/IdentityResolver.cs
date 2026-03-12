using System.Security.Claims;
using Overflow.EstimationService.Clients;

namespace Overflow.EstimationService.Auth;

/// <summary>
/// Resolves the participant identity from the current HTTP context.
/// For authenticated users, fetches the display name from the Profile Service
/// (falling back to JWT claims if the profile service is unreachable).
/// For guests, uses a stable cookie-based ID.
/// </summary>
public class IdentityResolver(ProfileServiceClient profileClient)
{
    public record ParticipantIdentity(
        string ParticipantId,
        string? UserId,
        string? GuestId,
        string DisplayName,
        bool IsGuest);

    public async Task<ParticipantIdentity> ResolveAsync(HttpContext ctx, string? guestDisplayName = null)
    {
        var userId = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is not null)
        {
            // Try Profile Service first (single source of truth for display names)
            var accessToken = ctx.Request.Headers.Authorization.ToString().Replace("Bearer ", "");
            var profileName = await profileClient.GetDisplayNameAsync(userId,
                string.IsNullOrWhiteSpace(accessToken) ? null : accessToken);

            // Fallback to JWT claims only if profile service is unreachable
            var name = profileName ?? FallbackNameFromClaims(ctx) ?? "User";

            return new ParticipantIdentity(userId, userId, null, name, false);
        }

        var guestId = GuestIdentity.GetGuestId(ctx);

        if (!string.IsNullOrEmpty(guestId))
        {
            return new ParticipantIdentity(guestId, null, guestId, guestDisplayName ?? "Guest", true);
        }

        // New guest — issue cookie
        var newGuestId = GuestIdentity.EnsureGuestId(ctx);
        return new ParticipantIdentity(newGuestId, null, newGuestId, guestDisplayName ?? "Guest", true);
    }

    private static string? FallbackNameFromClaims(HttpContext ctx)
    {
        var givenName = ctx.User.FindFirstValue("given_name") ?? "";
        var familyName = ctx.User.FindFirstValue("family_name") ?? "";
        var fullName = $"{givenName} {familyName}".Trim();

        return ctx.User.FindFirstValue("name")
               ?? ctx.User.FindFirstValue(ClaimTypes.Name)
               ?? (!string.IsNullOrWhiteSpace(fullName) ? fullName : null)
               ?? ctx.User.FindFirstValue("preferred_username");
    }
}