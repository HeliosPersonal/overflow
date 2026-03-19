using Microsoft.EntityFrameworkCore;
using Overflow.Contracts;
using Overflow.ProfileService.Data;

namespace Overflow.ProfileService.MessageHandlers;

public class UserReputationChangedHandler(ProfileDbContext db, ILogger<UserReputationChangedHandler> logger)
{
    public async Task Handle(UserReputationChanged message)
    {
        await db.UserProfiles.Where(x => x.Id == message.UserId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Reputation, 
                x => x.Reputation + message.Delta));
        
        logger.LogInformation("Reputation updated for user {UserId}: delta={Delta}, reason={Reason}", 
            message.UserId, message.Delta, message.Reason);
    }    
}