using CommandFlow;
using Microsoft.EntityFrameworkCore;
using Overflow.VoteService.Data;
using Overflow.VoteService.DTOs;

namespace Overflow.VoteService.Features.Votes.Queries;

public record GetUserVotesQuery(string UserId, string QuestionId) : IQuery<List<UserVotesResult>>;

public class GetUserVotesHandler(VoteDbContext db) : IRequestHandler<GetUserVotesQuery, List<UserVotesResult>>
{
    public async Task<List<UserVotesResult>> Handle(GetUserVotesQuery request, CancellationToken cancellationToken)
    {
        return await db.Votes
            .AsNoTracking()
            .Where(x => x.UserId == request.UserId && x.QuestionId == request.QuestionId)
            .Select(x => new UserVotesResult(x.TargetId, x.TargetType, x.VoteValue))
            .ToListAsync(cancellationToken);
    }
}