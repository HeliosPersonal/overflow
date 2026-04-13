using System.Security.Claims;
using Overflow.Common.CommonExtensions;
using Overflow.ProfileService.Data;
using Overflow.ProfileService.Models;

namespace Overflow.ProfileService.Middleware;

public class UserProfileCreationMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ProfileDbContext db)
    {
        if (context.User.Identity?.IsAuthenticated is true)
        {
            var userId = context.User.GetUserId();

            var givenName = context.User.FindFirstValue("given_name") ?? "";
            var familyName = context.User.FindFirstValue("family_name") ?? "";
            var fullName = $"{givenName} {familyName}".Trim();

            var name = context.User.FindFirstValue("name")
                       ?? context.User.FindFirstValue(ClaimTypes.Name)
                       ?? (!string.IsNullOrWhiteSpace(fullName) ? fullName : null)
                       ?? context.User.FindFirstValue("preferred_username")
                       ?? "Unnamed";

            var email = context.User.FindFirstValue(ClaimTypes.Email)
                       ?? context.User.FindFirstValue("email");

            if (userId is not null)
            {
                var profile = await db.UserProfiles.FindAsync(userId);
                if (profile is null)
                {
                    var newProfile = new UserProfile
                    {
                        Id = userId,
                        DisplayName = name,
                        Email = email,
                    };

                    db.UserProfiles.Add(newProfile);
                    await db.SaveChangesAsync();
                }
                else if (profile.Email is null && email is not null)
                {
                    // Backfill email for existing profiles
                    profile.Email = email;
                    await db.SaveChangesAsync();
                }
            }
        }

        await next(context);
    }
}