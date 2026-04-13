using CommandFlow;
using Microsoft.EntityFrameworkCore;
using Overflow.Common;
using Overflow.ProfileService.Data;
using Overflow.ProfileService.DTOs;

namespace Overflow.ProfileService.Features.Profiles.Queries;

public record GetAdminUsersQuery(string? Search, int? Page, int? PageSize)
    : IQuery<PaginationResult<ProfileDto>>;

public class GetAdminUsersHandler(ProfileDbContext db)
    : IRequestHandler<GetAdminUsersQuery, PaginationResult<ProfileDto>>
{
    public async Task<PaginationResult<ProfileDto>> Handle(GetAdminUsersQuery request,
        CancellationToken cancellationToken)
    {
        var query = db.UserProfiles.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = $"%{request.Search.Trim()}%";
            query = query.Where(x =>
                EF.Functions.ILike(x.DisplayName, search) ||
                (x.Email != null && EF.Functions.ILike(x.Email, search)));
        }

        query = query.OrderByDescending(x => x.Reputation);

        var paginationRequest = new PaginationRequest
        {
            Page = request.Page,
            PageSize = request.PageSize
        };

        var result = await query
            .Select(x => new ProfileDto(x.Id, x.DisplayName, x.Email, x.Description, x.AvatarUrl, x.Reputation,
                x.JoinedAt, x.ThemePreference))
            .ToPaginatedListAsync(paginationRequest);

        return result;
    }
}

