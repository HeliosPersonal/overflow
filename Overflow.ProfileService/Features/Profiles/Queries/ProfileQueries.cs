using CommandFlow;
using Microsoft.EntityFrameworkCore;
using Overflow.ProfileService.Data;
using Overflow.ProfileService.DTOs;

namespace Overflow.ProfileService.Features.Profiles.Queries;

// ─── Get Profiles (list) ─────────────────────────────────────────────────

public record GetProfilesQuery(string? SortBy) : IQuery<List<ProfileDto>>;

public class GetProfilesHandler(ProfileDbContext db) : IRequestHandler<GetProfilesQuery, List<ProfileDto>>
{
    private const string SortByReputation = "reputation";

    public async Task<List<ProfileDto>> Handle(GetProfilesQuery request, CancellationToken cancellationToken)
    {
        var query = db.UserProfiles.AsNoTracking().AsQueryable();
        query = request.SortBy == SortByReputation
            ? query.OrderByDescending(x => x.Reputation)
            : query.OrderBy(x => x.DisplayName);

        return await query
            .Select(x => new ProfileDto(x.Id, x.DisplayName, x.Description, x.AvatarUrl, x.Reputation, x.JoinedAt,
                x.ThemePreference))
            .ToListAsync(cancellationToken);
    }
}

// ─── Get Profile by ID ──────────────────────────────────────────────────

public record GetProfileByIdQuery(string Id) : IQuery<ProfileDto?>;

public class GetProfileByIdHandler(ProfileDbContext db) : IRequestHandler<GetProfileByIdQuery, ProfileDto?>
{
    public async Task<ProfileDto?> Handle(GetProfileByIdQuery request, CancellationToken cancellationToken)
    {
        var profile = await db.UserProfiles.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        return profile is null
            ? null
            : new ProfileDto(profile.Id, profile.DisplayName, profile.Description, profile.AvatarUrl,
                profile.Reputation, profile.JoinedAt, profile.ThemePreference);
    }
}

// ─── Get Batch ──────────────────────────────────────────────────────────

public record GetProfileBatchQuery(List<string> Ids) : IQuery<List<ProfileSummaryDto>>;

public class GetProfileBatchHandler(ProfileDbContext db)
    : IRequestHandler<GetProfileBatchQuery, List<ProfileSummaryDto>>
{
    public async Task<List<ProfileSummaryDto>> Handle(GetProfileBatchQuery request, CancellationToken cancellationToken)
    {
        return await db.UserProfiles
            .AsNoTracking()
            .Where(x => request.Ids.Contains(x.Id))
            .Select(x => new ProfileSummaryDto(x.Id, x.DisplayName, x.Reputation, x.AvatarUrl))
            .ToListAsync(cancellationToken);
    }
}