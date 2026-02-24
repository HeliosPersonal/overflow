using Marten;
using Overflow.Contracts;
using Wolverine.Attributes;

namespace Overflow.StatsService.MessageHandlers;

public class UserReputationChangeHandler
{
    [Transactional]
    public static async Task Handle(UserReputationChanged message, IDocumentSession session)
    {
        session.Events.Append(message.UserId, message);
        await session.SaveChangesAsync();
    }
}