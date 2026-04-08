using CommandFlow;
using CSharpFunctionalExtensions;
using Overflow.Common;
using Overflow.Contracts;
using Overflow.QuestionService.Data;
using Wolverine;
using ZiggyCreatures.Caching.Fusion;

namespace Overflow.QuestionService.Features.Questions.Commands;

public record DeleteAnswerCommand(string QuestionId, string AnswerId, string UserId, bool IsAdmin = false) : ICommand<Result>;

public class DeleteAnswerHandler(
    QuestionDbContext db,
    IMessageBus bus,
    IFusionCache cache,
    ILogger<DeleteAnswerHandler> logger) : IRequestHandler<DeleteAnswerCommand, Result>
{
    public async Task<Result> Handle(DeleteAnswerCommand request, CancellationToken ct)
    {
        var answer = await db.Answers.FindAsync([request.AnswerId], ct);
        var question = await db.Questions.FindAsync([request.QuestionId], ct);

        if (answer is null || question is null)
            return Result.Failure(DomainErrors.NotFound);

        if (answer.QuestionId != request.QuestionId || answer.Accepted)
            return Result.Failure(DomainErrors.Forbidden);

        if (!request.IsAdmin && answer.UserId != request.UserId)
            return Result.Failure(DomainErrors.Forbidden);

        db.Answers.Remove(answer);
        question.AnswerCount--;
        await db.SaveChangesAsync(ct);
        await bus.PublishAsync(new AnswerCountUpdated(request.QuestionId, question.AnswerCount));

        await cache.RemoveByTagAsync(CacheTags.QuestionList, token: ct);
        await cache.ExpireAsync(CacheKeys.QuestionDetail(request.QuestionId), token: ct);

        logger.LogInformation("Answer deleted: {AnswerId} from question {QuestionId}", request.AnswerId,
            request.QuestionId);
        return Result.Success();
    }
}