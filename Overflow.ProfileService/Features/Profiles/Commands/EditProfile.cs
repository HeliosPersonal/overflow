using CommandFlow;
using CSharpFunctionalExtensions;
using Overflow.ProfileService.Data;
using Overflow.ProfileService.Models;

namespace Overflow.ProfileService.Features.Profiles.Commands;

public record EditProfileCommand(
    string UserId,
    string? DisplayName,
    string? Description,
    string? AvatarUrl,
    ThemePreference? ThemePreference)
    : ICommand<Result>;

public class EditProfileHandler(ProfileDbContext db) : IRequestHandler<EditProfileCommand, Result>
{
    public async Task<Result> Handle(EditProfileCommand request, CancellationToken cancellationToken)
    {
        var profile = await db.UserProfiles.FindAsync([request.UserId], cancellationToken);
        if (profile is null) return Result.Failure(DomainErrors.ProfileNotFound);

        profile.DisplayName = request.DisplayName ?? profile.DisplayName;
        profile.Description = request.Description ?? profile.Description;
        profile.AvatarUrl = request.AvatarUrl ?? profile.AvatarUrl;
        if (request.ThemePreference.HasValue)
            profile.ThemePreference = request.ThemePreference.Value;
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}