using Microsoft.Extensions.Options;
using Overflow.Contracts;
using Overflow.SearchService.Options;
using Typesense;

namespace Overflow.SearchService.MessageHandlers;

public class AnswerCountUpdatedHandler(ITypesenseClient client, IOptions<TypesenseOptions> options)
{
    public async Task HandleAsync(AnswerCountUpdated message)
    {
        await client.UpdateDocument(options.Value.CollectionName, message.QuestionId,
            new { message.AnswerCount });
    }
}