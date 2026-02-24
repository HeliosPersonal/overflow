using System.Text.RegularExpressions;
using Overflow.Contracts;
using Overflow.SearchService.Models;
using Overflow.SearchService.Options;
using Typesense;

namespace Overflow.SearchService.MessageHandlers;

public class QuestionCreatedHandler(ITypesenseClient client, TypesenseOptions options, ILogger<QuestionCreatedHandler> logger)
{
    public async Task HandleAsync(QuestionCreated message)
    {
        var created = new DateTimeOffset(message.Created).ToUnixTimeSeconds();

        var doc = new SearchQuestion
        {
            Id = message.QuestionId,
            Title = message.Title,
            Content = StripHtml(message.Content),
            CreatedAt = created,
            Tags = message.Tags.ToArray(),
        };

        await client.CreateDocument(options.CollectionName, doc);
        logger.LogInformation("Indexed question in search: {QuestionId} with tags {Tags}",
            message.QuestionId, string.Join(", ", message.Tags));
    }

    private static string StripHtml(string content) => Regex.Replace(content, "<.*?>", string.Empty);
}