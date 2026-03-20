using CommandFlow;
using CSharpFunctionalExtensions;
using Ganss.Xss;
using Overflow.Common;
using Overflow.Contracts;
using Overflow.QuestionService.Data;
using Overflow.QuestionService.Models;
using Wolverine;
using ZiggyCreatures.Caching.Fusion;

namespace Overflow.QuestionService.Features.Questions.Commands;

public record PostAnswerCommand(string QuestionId, string Content, string UserId) : ICommand<Result<Answer>>;

public class PostAnswerHandler(
    QuestionDbContext db,
    IMessageBus bus,
    IHtmlSanitizer sanitizer,
    IFusionCache cache,
    ILogger<PostAnswerHandler> logger) : IRequestHandler<PostAnswerCommand, Result<Answer>>
{
    public async Task<Result<Answer>> Handle(PostAnswerCommand request, CancellationToken ct)
    {
        var question = await db.Questions.FindAsync([request.QuestionId], ct);
        if (question is null)
            return Result.Failure<Answer>(DomainErrors.QuestionNotFound);

        var answer = new Answer
        {
            Content = sanitizer.Sanitize(request.Content),
            UserId = request.UserId,
            QuestionId = request.QuestionId
        };

        question.Answers.Add(answer);
        question.AnswerCount++;
        await db.SaveChangesAsync(ct);
        await bus.PublishAsync(new AnswerCountUpdated(request.QuestionId, question.AnswerCount));

        await cache.RemoveByTagAsync(CacheTags.QuestionList, token: ct);
        await cache.ExpireAsync(CacheKeys.QuestionDetail(request.QuestionId), token: ct);

        logger.LogInformation("Answer posted: {AnswerId} to question {QuestionId} by {UserId}",
            answer.Id, request.QuestionId, request.UserId);

        return answer;
    }
}