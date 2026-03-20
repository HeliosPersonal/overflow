using JasperFx.Events;
using JasperFx.Events.Projections;
using Marten;
using Overflow.Common.CommonExtensions;
using Overflow.Contracts;
using Overflow.ServiceDefaults;
using Overflow.StatsService.Extensions;
using Overflow.StatsService.Models;
using Overflow.StatsService.Projections;

var builder = WebApplication.CreateBuilder(args);

builder.AddEnvVariablesAndConfigureSecrets();
builder.Services.AddControllers();
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

app.MapControllers();
app.MapDefaultEndpoints();

app.Run();