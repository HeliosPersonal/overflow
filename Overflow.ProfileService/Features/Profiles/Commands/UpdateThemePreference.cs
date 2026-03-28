using CommandFlow;
using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Overflow.ProfileService.Data;
using Overflow.ProfileService.Models;

namespace Overflow.ProfileService.Features.Profiles.Commands;

public record UpdateThemePreferenceCommand(string UserId, ThemePreference ThemePreference) : ICommand<Result>;

public class UpdateThemePreferenceHandler(ProfileDbContext db)
    : IRequestHandler<UpdateThemePreferenceCommand, Result>
{
    public async Task<Result> Handle(UpdateThemePreferenceCommand request, CancellationToken cancellationToken)
    {
        var rows = await db.UserProfiles
            .Where(p => p.Id == request.UserId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(p => p.ThemePreference, request.ThemePreference),
                cancellationToken);

        return rows > 0 ? Result.Success() : Result.Failure("Profile not found");
    }
}