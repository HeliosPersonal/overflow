using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Overflow.Contracts;
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
            Content = StripHtml(message.Content),
            Tags = message.Tags.ToArray(),
        });

        logger.LogDebug("Updated question in search index: {QuestionId}", message.QuestionId);
    }

    private static string StripHtml(string content) => Regex.Replace(content, "<.*?>", string.Empty);
}