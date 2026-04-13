using Microsoft.EntityFrameworkCore;
using Overflow.Contracts;
using Overflow.VoteService.Data;

namespace Overflow.VoteService.MessageHandlers;

public class UserDeletedHandler(VoteDbContext db, ILogger<UserDeletedHandler> logger)
{
    public async Task Handle(UserDeleted message)
    {
        var deleted = await db.Votes
            .Where(v => v.UserId == message.UserId)
            .ExecuteDeleteAsync();

        logger.LogInformation("UserDeleted cleanup: removed {Count} vote(s) for user {UserId}",
            deleted, message.UserId);
    }
}

