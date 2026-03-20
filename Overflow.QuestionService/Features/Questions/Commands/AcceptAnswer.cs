using CommandFlow;
using CSharpFunctionalExtensions;
using Overflow.Common;
using Overflow.Contracts;
using Overflow.Contracts.Helpers;
using Overflow.QuestionService.Data;
using Wolverine;
using ZiggyCreatures.Caching.Fusion;

namespace Overflow.QuestionService.Features.Questions.Commands;

public record AcceptAnswerCommand(string QuestionId, string AnswerId) : ICommand<Result>;

public class AcceptAnswerHandler(
    QuestionDbContext db,
    IMessageBus bus,
    IFusionCache cache,
    ILogger<AcceptAnswerHandler> logger) : IRequestHandler<AcceptAnswerCommand, Result>
{
    public async Task<Result> Handle(AcceptAnswerCommand request, CancellationToken ct)
    {
        var answer = await db.Answers.FindAsync([request.AnswerId], ct);
        var question = await db.Questions.FindAsync([request.QuestionId], ct);

        if (answer is null || question is null)
            return Result.Failure(DomainErrors.NotFound);

        if (answer.QuestionId != request.QuestionId || question.HasAcceptedAnswer)
            return Result.Failure(DomainErrors.Forbidden);

        answer.Accepted = true;
        question.HasAcceptedAnswer = true;
        await db.SaveChangesAsync(ct);

        await bus.PublishAsync(new AnswerAccepted(request.QuestionId));
        await bus.PublishAsync(ReputationHelper.MakeEvent(answer.UserId,
            ReputationReason.AnswerAccepted, question.AskerId));

        await cache.RemoveByTagAsync(CacheTags.QuestionList, token: ct);
        await cache.ExpireAsync(CacheKeys.QuestionDetail(request.QuestionId), token: ct);

        logger.LogInformation("Answer accepted: {AnswerId} for question {QuestionId}, user {UserId} gained reputation",
            request.AnswerId, request.QuestionId, answer.UserId);

        return Result.Success();
    }
}