using JasperFx.Events;
using JasperFx.Events.Projections;
using Marten;
using Overflow.Common;
using Overflow.Common.CommonExtensions;
using Overflow.Contracts;
using Overflow.ServiceDefaults;
using Overflow.StatsService.Extensions;
using Overflow.StatsService.Models;
using Overflow.StatsService.Projections;
using ZiggyCreatures.Caching.Fusion;

var builder = WebApplication.CreateBuilder(args);

builder.AddEnvVariablesAndConfigureSecrets();
builder.Services.AddOpenApi();
builder.AddServiceDefaults();

builder.Services.AddHealthChecks()
    .AddRabbitMqHealthCheck();

await builder.UseWolverineWithRabbitMqAsync(opts => { opts.ApplicationAssembly = typeof(Program).Assembly; });

var connString = builder.Configuration.GetConnectionString("statDb")!;
await connString.EnsurePostgresDatabaseExistsAsync();

builder.Services.AddMarten(opts =>
{
    opts.Connection(connString);
    opts.Events.StreamIdentity = StreamIdentity.AsString;
    opts.Events.AddEventType<QuestionCreated>();
    opts.Events.AddEventType<UserReputationChanged>();

    opts.Schema.For<TagDailyUsage>()
        .Index(x => x.Tag)
        .Index(x => x.Date);

    opts.Schema.For<UserReputationChanged>()
        .Index(x => x.UserId)
        .Index(x => x.Occurred);

    opts.Projections.Add(new TrendingTagsProjection(), ProjectionLifecycle.Inline);
    opts.Projections.Add(new TopUsersProjection(), ProjectionLifecycle.Inline);
}).UseLightweightSessions();

builder.AddFusionCacheWithRedis();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/stats/trending-tags", async (IQuerySession session, IFusionCache cache, ILogger<Program> logger) =>
{
    var result = await cache.GetOrSetAsync(CacheKeys.TrendingTags, async _ =>
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var start = today.AddDays(-6);

        var rows = await session.Query<TagDailyUsage>()
            .Where(x => x.Date >= start && x.Date <= today)
            .Select(x => new { x.Tag, x.Count })
            .ToListAsync();

        return rows
            .GroupBy(x => x.Tag)
            .Select(x => new { tag = x.Key, count = x.Sum(t => t.Count) })
            .OrderByDescending(x => x.count)
            .Take(5)
            .ToList();
    }, tags: [CacheTags.TrendingTags]);

    logger.LogDebug("Trending tags retrieved: {Count} tags over last 7 days", result.Count);
    return Results.Ok(result);
});

app.MapGet("/stats/top-users", async (IQuerySession session, IFusionCache cache, ILogger<Program> logger) =>
{
    var result = await cache.GetOrSetAsync(CacheKeys.TopUsers, async _ =>
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var start = today.AddDays(-6);

        var rows = await session.Query<UserDailyReputation>()
            .Where(x => x.Date >= start && x.Date <= today)
            .Select(x => new { x.UserId, x.Delta })
            .ToListAsync();

        return rows.GroupBy(x => x.UserId)
            .Select(g => new { userId = g.Key, delta = g.Sum(t => t.Delta) })
            .OrderByDescending(x => x.delta)
            .Take(5)
            .ToList();
    }, tags: [CacheTags.TopUsers]);

    logger.LogDebug("Top users retrieved: {Count} users by reputation delta over last 7 days", result.Count);
    return Results.Ok(result);
});

app.MapDefaultEndpoints();

app.Run();