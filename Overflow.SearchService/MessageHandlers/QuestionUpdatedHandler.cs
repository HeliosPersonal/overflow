using System.Text.RegularExpressions;
using Overflow.Contracts;
using Typesense;

namespace Overflow.SearchService.MessageHandlers;

public class QuestionUpdatedHandler(ITypesenseClient client, ILogger<QuestionUpdatedHandler> logger)
{
    public async Task HandleAsync(QuestionUpdated message)
    {
        await client.UpdateDocument("questions", message.QuestionId, new
        {
            message.Title,
            Content = StripHtml(message.Content),
            Tags = message.Tags.ToArray(),
        });
        
        logger.LogDebug("Updated question in search index: {QuestionId}", message.QuestionId);
    }
    
    private static string StripHtml(string content)
    {
        return Regex.Replace(content, "<.*?>", string.Empty);
    }
}