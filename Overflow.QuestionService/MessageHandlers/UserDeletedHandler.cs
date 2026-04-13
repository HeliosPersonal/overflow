using Microsoft.EntityFrameworkCore;
using Overflow.Common;
using Overflow.Contracts;
using Overflow.QuestionService.Data;
using Wolverine;
using ZiggyCreatures.Caching.Fusion;

namespace Overflow.QuestionService.MessageHandlers;

public class UserDeletedHandler(
    QuestionDbContext db,
    IMessageBus bus,
    IFusionCache cache,
    ILogger<UserDeletedHandler> logger)
{
    public async Task Handle(UserDeleted message)
    {
        var userId = message.UserId;
        logger.LogInformation("Handling UserDeleted for {UserId} — cleaning up questions & answers", userId);

        // 1. Find all questions by this user (need IDs for QuestionDeleted events)
        var questionIds = await db.Questions
            .Where(q => q.AskerId == userId)
            .Select(q => q.Id)
            .ToListAsync();

        // 2. Delete answers by this user on OTHER people's questions
        var deletedAnswers = await db.Answers
            .Where(a => a.UserId == userId && !questionIds.Contains(a.QuestionId))
            .ExecuteDeleteAsync();

        // 3. Update answer counts on questions where this user's answers were removed
        // (the answers are already deleted, so recalculate from remaining)
        if (deletedAnswers > 0)
        {
            // Re-count answers for affected questions
            var affectedQuestionIds = await db.Questions
                .Where(q => q.Answers.Any() || q.AnswerCount > 0)
                .Select(q => new { q.Id, Count = q.Answers.Count })
                .ToListAsync();

            foreach (var aq in affectedQuestionIds)
            {
                await db.Questions.Where(q => q.Id == aq.Id)
                    .ExecuteUpdateAsync(s => s.SetProperty(q => q.AnswerCount, aq.Count));
            }
        }

        // 4. Delete all questions by this user (cascading to their answers)
        var deletedQuestions = await db.Questions
            .Where(q => q.AskerId == userId)
            .ExecuteDeleteAsync();

        // 5. Publish QuestionDeleted for each question so SearchService/StatsService clean up
        foreach (var questionId in questionIds)
        {
            await bus.PublishAsync(new QuestionDeleted(questionId));
        }

        // 6. Invalidate caches
        await cache.RemoveByTagAsync(CacheTags.QuestionList);

        logger.LogInformation(
            "UserDeleted cleanup complete for {UserId}: {QuestionCount} questions, {AnswerCount} orphan answers removed",
            userId, deletedQuestions, deletedAnswers);
    }
}

