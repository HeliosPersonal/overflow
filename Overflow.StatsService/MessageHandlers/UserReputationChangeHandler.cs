using Marten;
using Overflow.Common;
using Overflow.Contracts;
using Wolverine.Attributes;
using ZiggyCreatures.Caching.Fusion;

namespace Overflow.StatsService.MessageHandlers;

public class UserReputationChangeHandler
{
    [Transactional]
    public static async Task Handle(UserReputationChanged message, IDocumentSession session, IFusionCache cache)
    {
        session.Events.Append(message.UserId, message);
        await session.SaveChangesAsync();
        await cache.RemoveByTagAsync(CacheTags.TopUsers);
    }
}