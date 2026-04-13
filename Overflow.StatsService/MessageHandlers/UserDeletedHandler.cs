using Overflow.Common;
using Overflow.Contracts;
using ZiggyCreatures.Caching.Fusion;

namespace Overflow.StatsService.MessageHandlers;

public class UserDeletedHandler
{
    public static async Task Handle(UserDeleted message, IFusionCache cache, ILogger<UserDeletedHandler> logger)
    {
        await cache.RemoveByTagAsync(CacheTags.TopUsers);
        logger.LogInformation("UserDeleted cleanup: invalidated top-users cache for {UserId}", message.UserId);
    }
}


