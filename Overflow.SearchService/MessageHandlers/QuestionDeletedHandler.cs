using Overflow.Contracts;
using Overflow.SearchService.Models;
using Typesense;

namespace Overflow.SearchService.MessageHandlers;

public class QuestionDeletedHandler(ITypesenseClient client)
{
    public async Task HandleAsync(QuestionDeleted message)
    {
        await client.DeleteDocument<SearchQuestion>("questions", message.QuestionId);
    }
}