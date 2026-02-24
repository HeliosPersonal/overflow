using Microsoft.EntityFrameworkCore;
using Overflow.Contracts;
using Overflow.QuestionService.Data;

namespace Overflow.QuestionService.MessageHandlers;

public class VoteCastedHandler(QuestionDbContext db, ILogger<VoteCastedHandler> logger)
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
    }
}