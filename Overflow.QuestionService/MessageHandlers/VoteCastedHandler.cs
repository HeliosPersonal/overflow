using Microsoft.EntityFrameworkCore;
using Overflow.Common;
using Overflow.Contracts;
using Overflow.QuestionService.Data;
using ZiggyCreatures.Caching.Fusion;

namespace Overflow.QuestionService.MessageHandlers;

public class VoteCastedHandler(QuestionDbContext db, IFusionCache cache)
{
    public async Task Handle(VoteCasted message)
    {
        if (message.TargetType == VoteTargetType.Question)
        {
            await db.Questions
                .Where(x => x.Id == message.TargetId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Votes,
                    x => x.Votes + message.VoteValue));
        }
        else if (message.TargetType == VoteTargetType.Answer)
        {
            await db.Answers
                .Where(x => x.Id == message.TargetId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Votes,
                    x => x.Votes + message.VoteValue));
        }

        await cache.RemoveByTagAsync(CacheTags.QuestionList);
    }
}