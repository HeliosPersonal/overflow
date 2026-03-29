using CommandFlow;
using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Overflow.Contracts;
using Overflow.Contracts.Helpers;
using Overflow.VoteService.Data;
using Overflow.VoteService.Models;
using Wolverine;

namespace Overflow.VoteService.Features.Votes.Commands;

public record CastVoteCommand(
    string UserId,
    string TargetId,
    string TargetType,
    string TargetUserId,
    string QuestionId,
    int VoteValue) : ICommand<Result>;

public class CastVoteHandler(VoteDbContext db, IMessageBus bus) : IRequestHandler<CastVoteCommand, Result>
{
    public async Task<Result> Handle(CastVoteCommand request, CancellationToken cancellationToken)
    {
        if (!VoteTargetType.IsValid(request.TargetType))
            return Result.Failure(DomainErrors.InvalidTargetType);

        var alreadyVoted = await db.Votes.AsNoTracking()
            .AnyAsync(x => x.UserId == request.UserId && x.TargetId == request.TargetId, cancellationToken);
        if (alreadyVoted)
            return Result.Failure(DomainErrors.AlreadyVoted);

        db.Votes.Add(new Vote
        {
            TargetId = request.TargetId,
            TargetType = request.TargetType,
            UserId = request.UserId,
            VoteValue = request.VoteValue,
            QuestionId = request.QuestionId
        });

        await db.SaveChangesAsync(cancellationToken);

        var reason = (request.VoteValue, request.TargetType) switch
        {
            (1, VoteTargetType.Question) => ReputationReason.QuestionUpvoted,
            (1, VoteTargetType.Answer) => ReputationReason.AnswerUpvoted,
            (-1, VoteTargetType.Answer) => ReputationReason.AnswerDownvoted,
            _ => ReputationReason.QuestionDownvoted
        };

        await bus.PublishAsync(ReputationHelper.MakeEvent(request.TargetUserId, reason, request.UserId));
        await bus.PublishAsync(new VoteCasted(request.TargetId, request.TargetType, request.VoteValue));

        return Result.Success();
    }
}