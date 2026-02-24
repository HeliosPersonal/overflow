using Typesense;

namespace Overflow.SearchService.Data;

public static class SearchInitializer
{
    public static async Task EnsureIndexExists(ITypesenseClient client, string collectionName, ILogger? logger = null)
    {
        try
        {
            await client.RetrieveCollection(collectionName);
            logger?.LogDebug("Typesense collection already exists: {Collection}", collectionName);
            return;
        }
        catch (TypesenseApiNotFoundException)
        {
            logger?.LogInformation("Creating Typesense collection: {Collection}", collectionName);
        }

        var schema = new Schema(collectionName, new List<Field>
        {
            new("id", FieldType.String),
            new("title", FieldType.String),
            new("content", FieldType.String),
            new("tags", FieldType.StringArray),
            new("createdAt", FieldType.Int64),
            new("answerCount", FieldType.Int32),
            new("hasAcceptedAnswer", FieldType.Bool),
        })
        {
            DefaultSortingField = "createdAt"
        };

        await client.CreateCollection(schema);
        logger?.LogInformation("Created Typesense collection: {Collection}", collectionName);
    }
}