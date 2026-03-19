using Microsoft.Extensions.Options;
using Overflow.Contracts;
using Overflow.SearchService.Models;
using Overflow.SearchService.Options;
using Typesense;

namespace Overflow.SearchService.MessageHandlers;

public class QuestionDeletedHandler(
    ITypesenseClient client,
    IOptions<TypesenseOptions> options,
    ILogger<QuestionDeletedHandler> logger)
{
    public async Task HandleAsync(QuestionDeleted message)
    {
        await client.DeleteDocument<SearchQuestion>(options.Value.CollectionName, message.QuestionId);
        logger.LogInformation("Deleted question from search index: {QuestionId}", message.QuestionId);
    }
}