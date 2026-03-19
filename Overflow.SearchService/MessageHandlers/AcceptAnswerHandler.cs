using Microsoft.Extensions.Options;
using Overflow.Contracts;
using Overflow.SearchService.Options;
using Typesense;

namespace Overflow.SearchService.MessageHandlers;

public class AcceptAnswerHandler(ITypesenseClient client, IOptions<TypesenseOptions> options)
{
    public async Task HandleAsync(AnswerAccepted message)
    {
        await client.UpdateDocument(options.Value.CollectionName, message.QuestionId,
            new { HasAcceptedAnswer = true });
    }
}