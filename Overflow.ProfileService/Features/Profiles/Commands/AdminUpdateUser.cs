using CommandFlow;
using CSharpFunctionalExtensions;
using Overflow.ProfileService.Data;
using Overflow.ProfileService.DTOs;

namespace Overflow.ProfileService.Features.Profiles.Commands;

public record AdminUpdateUserCommand(string UserId, string? DisplayName, string? Description, string? AvatarUrl)
    : ICommand<Result<ProfileDto>>;

public class AdminUpdateUserHandler(ProfileDbContext db, ILogger<AdminUpdateUserHandler> logger)
    : IRequestHandler<AdminUpdateUserCommand, Result<ProfileDto>>
{
    public async Task<Result<ProfileDto>> Handle(AdminUpdateUserCommand request, CancellationToken cancellationToken)
    {
        var profile = await db.UserProfiles.FindAsync([request.UserId], cancellationToken);
        if (profile is null)
        {
            logger.LogWarning("Admin update failed — profile not found for user {UserId}", request.UserId);
            return Result.Failure<ProfileDto>(DomainErrors.ProfileNotFound);
        }

        if (request.DisplayName is not null)
            profile.DisplayName = request.DisplayName;
        if (request.Description is not null)
            profile.Description = request.Description;
        if (request.AvatarUrl is not null)
            profile.AvatarUrl = request.AvatarUrl;

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Admin updated profile for user {UserId}: DisplayName={DisplayName}",
            request.UserId, profile.DisplayName);

        return new ProfileDto(profile.Id, profile.DisplayName, profile.Email, profile.Description, profile.AvatarUrl,
            profile.Reputation, profile.JoinedAt, profile.ThemePreference);
    }
}

