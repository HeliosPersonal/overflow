using Microsoft.EntityFrameworkCore;
using Overflow.Contracts;
using Overflow.ProfileService.Data;

namespace Overflow.ProfileService.MessageHandlers;

public class UserReputationChangedHandler(ProfileDbContext db)
{
    public async Task Handle(UserReputationChanged message)
    {
        await db.UserProfiles.Where(x => x.Id == message.UserId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Reputation, 
                x => x.Reputation + message.Delta));
    }    
}