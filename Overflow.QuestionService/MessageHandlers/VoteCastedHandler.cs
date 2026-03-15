using Microsoft.EntityFrameworkCore;
using Overflow.Common;
using Overflow.Contracts;
using Overflow.QuestionService.Data;
using ZiggyCreatures.Caching.Fusion;

namespace Overflow.QuestionService.MessageHandlers;

public class VoteCastedHandler(QuestionDbContext db, IFusionCache cache, ILogger<VoteCastedHandler> logger)
{
    public async Task Handle(VoteCasted message)
    {
        if (message.TargetType == "Question")
        {
            await db.Questions
                .Where(x => x.Id == message.TargetId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Votes,
                    x => x.Votes + message.VoteValue));

            logger.LogDebug("Vote applied to question {QuestionId}: {VoteValue}",
                message.TargetId, message.VoteValue);
        }
        else if (message.TargetType == "Answer")
        {
            await db.Answers
                .Where(x => x.Id == message.TargetId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Votes,
                    x => x.Votes + message.VoteValue));

            logger.LogDebug("Vote applied to answer {AnswerId}: {VoteValue}",
                message.TargetId, message.VoteValue);
        }

        await cache.RemoveByTagAsync(CacheTags.QuestionList);
        logger.LogDebug("Question list cache invalidated after vote on {TargetType} {TargetId}",
            message.TargetType, message.TargetId);
    }
}