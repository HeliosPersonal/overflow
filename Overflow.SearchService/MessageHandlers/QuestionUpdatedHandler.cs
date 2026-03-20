using Microsoft.Extensions.Options;
using Overflow.Contracts;
using Overflow.SearchService.Extensions;
using Overflow.SearchService.Options;
using Typesense;

namespace Overflow.SearchService.MessageHandlers;

public class QuestionUpdatedHandler(
    ITypesenseClient client,
    IOptions<TypesenseOptions> options,
    ILogger<QuestionUpdatedHandler> logger)
{
    public async Task HandleAsync(QuestionUpdated message)
    {
        await client.UpdateDocument(options.Value.CollectionName, message.QuestionId, new
        {
            message.Title,
            Content = HtmlHelpers.StripTags(message.Content),
            Tags = message.Tags.ToArray(),
        });

        logger.LogDebug("Updated question in search index: {QuestionId}", message.QuestionId);
    }
}