using System.Security.Claims;
using Overflow.ProfileService.Data;
using Overflow.ProfileService.Models;

namespace Overflow.ProfileService.Middleware;

public class UserProfileCreationMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ProfileDbContext db)
    {
        if (context.User.Identity?.IsAuthenticated is true)
        {
            var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);

            var givenName = context.User.FindFirstValue("given_name") ?? "";
            var familyName = context.User.FindFirstValue("family_name") ?? "";
            var fullName = $"{givenName} {familyName}".Trim();

            var name = context.User.FindFirstValue("name")
                       ?? context.User.FindFirstValue(ClaimTypes.Name)
                       ?? (!string.IsNullOrWhiteSpace(fullName) ? fullName : null)
                       ?? context.User.FindFirstValue("preferred_username")
                       ?? "Unnamed";

            if (userId is not null)
            {
                var profile = await db.UserProfiles.FindAsync(userId);
                if (profile is null)
                {
                    var newProfile = new UserProfile
                    {
                        Id = userId,
                        DisplayName = name,
                    };

                    db.UserProfiles.Add(newProfile);
                    await db.SaveChangesAsync();
                }
            }
        }

        await next(context);
    }
}