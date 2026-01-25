using Overflow.Contracts;
using Overflow.SearchService.Models;
using Typesense;

namespace Overflow.SearchService.MessageHandlers;

public class QuestionDeletedHandler(ITypesenseClient client, ILogger<QuestionDeletedHandler> logger)
{
    public async Task HandleAsync(QuestionDeleted message)
    {
        await client.DeleteDocument<SearchQuestion>("questions", message.QuestionId);
        logger.LogInformation("Deleted question from search index: {QuestionId}", message.QuestionId);
    }
}