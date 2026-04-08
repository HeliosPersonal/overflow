using CommandFlow;
using CSharpFunctionalExtensions;
using Overflow.Common;
using Overflow.Contracts;
using Overflow.QuestionService.Data;
using Wolverine;
using ZiggyCreatures.Caching.Fusion;

namespace Overflow.QuestionService.Features.Questions.Commands;

public record DeleteQuestionCommand(string QuestionId, string UserId, bool IsAdmin = false) : ICommand<Result>;

public class DeleteQuestionHandler(
    QuestionDbContext db,
    IMessageBus bus,
    IFusionCache cache,
    ILogger<DeleteQuestionHandler> logger) : IRequestHandler<DeleteQuestionCommand, Result>
{
    public async Task<Result> Handle(DeleteQuestionCommand request, CancellationToken ct)
    {
        var question = await db.Questions.FindAsync([request.QuestionId], ct);
        if (question is null)
            return Result.Failure(DomainErrors.QuestionNotFound);

        if (!request.IsAdmin && request.UserId != question.AskerId)
            return Result.Failure(DomainErrors.Forbidden);

        db.Questions.Remove(question);
        await db.SaveChangesAsync(ct);
        await bus.PublishAsync(new QuestionDeleted(question.Id));

        await cache.RemoveByTagAsync(CacheTags.QuestionList, token: ct);
        await cache.RemoveAsync(CacheKeys.QuestionDetail(request.QuestionId), token: ct);

        logger.LogInformation("Question deleted: {QuestionId} by {UserId}", request.QuestionId, request.UserId);
        return Result.Success();
    }
}