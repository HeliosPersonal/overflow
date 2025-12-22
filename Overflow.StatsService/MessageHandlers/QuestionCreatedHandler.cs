using Marten;
using Overflow.Contracts;
using Wolverine.Attributes;

namespace Overflow.StatsService.MessageHandlers;

public class QuestionCreatedHandler
{
    [Transactional]
    public static async Task Handle(QuestionCreated message, IDocumentSession session, CancellationToken ct)
    {
        session.Events.StartStream(message.QuestionId, message);
        
        await session.SaveChangesAsync(ct);
    }
}