using CommandFlow;
using CSharpFunctionalExtensions;
using Ganss.Xss;
using Microsoft.EntityFrameworkCore;
using Overflow.Common;
using Overflow.Contracts;
using Overflow.QuestionService.Data;
using Overflow.QuestionService.Models;
using Overflow.QuestionService.Services;
using Wolverine;
using ZiggyCreatures.Caching.Fusion;

namespace Overflow.QuestionService.Features.Questions.Commands;

public record UpdateQuestionCommand(
    string QuestionId,
    string Title,
    string Content,
    List<string> Tags,
    string UserId) : ICommand<Result>;

public class UpdateQuestionHandler(
    QuestionDbContext db,
    IMessageBus bus,
    TagService tagService,
    IHtmlSanitizer sanitizer,
    IFusionCache cache,
    ILogger<UpdateQuestionHandler> logger) : IRequestHandler<UpdateQuestionCommand, Result>
{
    public async Task<Result> Handle(UpdateQuestionCommand request, CancellationToken ct)
    {
        var question = await db.Questions.FindAsync([request.QuestionId], ct);
        if (question is null)
            return Result.Failure(DomainErrors.QuestionNotFound);

        if (request.UserId != question.AskerId)
            return Result.Failure(DomainErrors.Forbidden);

        if (!await tagService.AreTagsValidAsync(request.Tags))
            return Result.Failure(DomainErrors.InvalidTags);

        var original = question.TagSlugs.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var incoming = request.Tags.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var removed = original.Except(incoming, StringComparer.OrdinalIgnoreCase).ToArray();
        var added = incoming.Except(original, StringComparer.OrdinalIgnoreCase).ToArray();

        question.Title = request.Title;
        question.Content = sanitizer.Sanitize(request.Content);
        question.TagSlugs = request.Tags;
        question.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        if (removed.Length > 0)
        {
            await db.Tags
                .Where(t => removed.Contains(t.Slug) && t.UsageCount > 0)
                .ExecuteUpdateAsync(x => x.SetProperty(t => t.UsageCount, t => t.UsageCount - 1), ct);
        }

        if (added.Length > 0)
        {
            await db.Tags
                .Where(t => added.Contains(t.Slug))
                .ExecuteUpdateAsync(x => x.SetProperty(t => t.UsageCount, t => t.UsageCount + 1), ct);
        }

        await bus.PublishAsync(new QuestionUpdated(question.Id, question.Title, question.Content,
            question.TagSlugs.ToArray()));

        await cache.RemoveByTagAsync(CacheTags.QuestionList, token: ct);
        await cache.ExpireAsync(CacheKeys.QuestionDetail(request.QuestionId), token: ct);

        logger.LogInformation("Question updated: {QuestionId} by {UserId}, tags added: {Added}, removed: {Removed}",
            request.QuestionId, request.UserId, string.Join(", ", added), string.Join(", ", removed));

        return Result.Success();
    }
}