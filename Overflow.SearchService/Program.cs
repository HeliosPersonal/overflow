using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Overflow.Common.CommonExtensions;
using Overflow.SearchService.Data;
using Overflow.SearchService.Extensions;
using Overflow.SearchService.Models;
using Overflow.ServiceDefaults;
using Typesense;
using Overflow.SearchService.Options;

var builder = WebApplication.CreateBuilder(args);

builder.AddEnvVariablesAndConfigureSecrets();
builder.Services.AddOpenApi();
builder.AddServiceDefaults();
builder.Services.AddTypesenseConfiguration(builder.Configuration);

builder.Services.AddHealthChecks()
    .AddTypesenseHealthCheck()
    .AddRabbitMqHealthCheck();

await builder.UseWolverineWithRabbitMqAsync(opts => { opts.ApplicationAssembly = typeof(Program).Assembly; });

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapDefaultEndpoints();

var typesenseOptions = app.Services.GetRequiredService<IOptions<TypesenseOptions>>().Value;

app.MapGet("/search", async (string query, ITypesenseClient client, ILogger<Program> logger) =>
{
    if (string.IsNullOrWhiteSpace(query))
    {
        logger.LogWarning("Search attempted with empty query");
        return Results.BadRequest("Query parameter is required");
    }

    string? tag = null;
    var tagMatch = Regex.Match(query, @"\[(.*?)\]");
    if (tagMatch.Success)
    {
        tag = tagMatch.Groups[1].Value;
        query = query.Replace(tagMatch.Value, "").Trim();
        logger.LogDebug("Extracted tag filter: {Tag} from query", tag);
    }

    var searchParams = new SearchParameters(query, "title,content");
    if (!string.IsNullOrWhiteSpace(tag))
    {
        searchParams.FilterBy = $"tags:=[{tag}]";
    }

    try
    {
        var result = await client.Search<SearchQuestion>(typesenseOptions.CollectionName, searchParams);
        logger.LogInformation("Search completed: query='{Query}', tag='{Tag}', found={Count}",
            query, tag, result.Found);
        return Results.Ok(result.Hits.Select(hit => hit.Document));
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Typesense search failed for query: {Query}", query);
        return Results.Problem("Search failed", ex.Message);
    }
});

app.MapGet("/search/similar-titles", async (string query, ITypesenseClient client, ILogger<Program> logger) =>
{
    if (string.IsNullOrWhiteSpace(query))
    {
        logger.LogWarning("Similar titles search attempted with empty query");
        return Results.BadRequest("Query parameter is required");
    }

    var searchParams = new SearchParameters(query, "title");

    try
    {
        var result = await client.Search<SearchQuestion>(typesenseOptions.CollectionName, searchParams);
        logger.LogDebug("Similar titles search: query='{Query}', found={Count}", query, result.Found);
        return Results.Ok(result.Hits.Select(hit => hit.Document));
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Similar titles search failed for query: {Query}", query);
        return Results.Problem("Search failed", ex.Message);
    }
});

using var scope = app.Services.CreateScope();
var client = scope.ServiceProvider.GetRequiredService<ITypesenseClient>();
await SearchInitializer.EnsureIndexExists(client, typesenseOptions.CollectionName);

app.Run();