using Overflow.Contracts;
using Typesense;

namespace Overflow.SearchService.MessageHandlers;

public class AcceptAnswerHandler(ITypesenseClient client)
{
    public async Task HandleAsync(AnswerAccepted message)
    {
        await client.UpdateDocument("questions", message.QuestionId, 
            new {HasAcceptedAnswer = true});
    }
}