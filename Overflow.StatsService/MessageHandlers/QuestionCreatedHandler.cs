using Marten;
using Overflow.Common;
using Overflow.Contracts;
using Wolverine.Attributes;
using ZiggyCreatures.Caching.Fusion;

namespace Overflow.StatsService.MessageHandlers;

public class QuestionCreatedHandler
{
    [Transactional]
    public static async Task Handle(QuestionCreated message, IDocumentSession session, IFusionCache cache,
        CancellationToken ct)
    {
        session.Events.StartStream(message.QuestionId, message);

        await session.SaveChangesAsync(ct);
        await cache.RemoveByTagAsync(CacheTags.TrendingTags, token: ct);
    }
}