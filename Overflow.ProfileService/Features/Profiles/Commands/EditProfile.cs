using CommandFlow;
using CSharpFunctionalExtensions;
using Overflow.ProfileService.Data;

namespace Overflow.ProfileService.Features.Profiles.Commands;

public record EditProfileCommand(string UserId, string? DisplayName, string? Description, string? AvatarUrl)
    : ICommand<Result>;

public class EditProfileHandler(ProfileDbContext db) : IRequestHandler<EditProfileCommand, Result>
{
    public async Task<Result> Handle(EditProfileCommand request, CancellationToken cancellationToken)
    {
        var profile = await db.UserProfiles.FindAsync([request.UserId], cancellationToken);
        if (profile is null) return Result.Failure("Profile not found");

        profile.DisplayName = request.DisplayName ?? profile.DisplayName;
        profile.Description = request.Description ?? profile.Description;
        profile.AvatarUrl = request.AvatarUrl ?? profile.AvatarUrl;
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}