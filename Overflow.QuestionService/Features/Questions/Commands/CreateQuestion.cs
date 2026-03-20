using CommandFlow;
using CSharpFunctionalExtensions;
using Ganss.Xss;
using Microsoft.EntityFrameworkCore;
using Overflow.Common;
using Overflow.Contracts;
using Overflow.QuestionService.Data;
using Overflow.QuestionService.DTOs;
using Overflow.QuestionService.Models;
using Overflow.QuestionService.Services;
using Wolverine;
using ZiggyCreatures.Caching.Fusion;

namespace Overflow.QuestionService.Features.Questions.Commands;

public record CreateQuestionCommand(string Title, string Content, List<string> Tags, string UserId)
    : ICommand<Result<Question>>;

public class CreateQuestionHandler(
    QuestionDbContext db,
    IMessageBus bus,
    TagService tagService,
    IHtmlSanitizer sanitizer,
    IFusionCache cache,
    ILogger<CreateQuestionHandler> logger) : IRequestHandler<CreateQuestionCommand, Result<Question>>
{
    public async Task<Result<Question>> Handle(CreateQuestionCommand request, CancellationToken cancellationToken)
    {
        if (!await tagService.AreTagsValidAsync(request.Tags))
        {
            logger.LogWarning("Question creation failed: invalid tags {Tags} for user {UserId}",
                string.Join(", ", request.Tags), request.UserId);
            return Result.Failure<Question>(DomainErrors.InvalidTags);
        }

        var question = new Question
        {
            Title = request.Title,
            Content = sanitizer.Sanitize(request.Content),
            TagSlugs = request.Tags,
            AskerId = request.UserId
        };

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            db.Questions.Add(question);
            await db.SaveChangesAsync(cancellationToken);

            await bus.PublishAsync(new QuestionCreated(question.Id, question.Title, question.Content,
                question.CreatedAt, question.TagSlugs));

            await tx.CommitAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(cancellationToken);
            logger.LogError(ex, "Failed to create question for user {UserId}", request.UserId);
            throw;
        }

        var slugs = question.TagSlugs.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (slugs.Length > 0)
        {
            await db.Tags
                .Where(t => slugs.Contains(t.Slug))
                .ExecuteUpdateAsync(x => x.SetProperty(t => t.UsageCount, t => t.UsageCount + 1), cancellationToken);
        }

        await cache.RemoveByTagAsync(CacheTags.QuestionList, token: cancellationToken);

        logger.LogInformation("Question created: {QuestionId} by {UserId} with tags {Tags}",
            question.Id, request.UserId, string.Join(", ", request.Tags));

        return question;
    }
}