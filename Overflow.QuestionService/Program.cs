using Ganss.Xss;
using Microsoft.EntityFrameworkCore;
using Overflow.Common.CommonExtensions;
using Overflow.Contracts;
using Overflow.QuestionService.Data;
using Overflow.QuestionService.Services;
using Overflow.ServiceDefaults;
using Wolverine.EntityFrameworkCore;
using Wolverine.Postgresql;
using Wolverine.RabbitMQ;

var builder = WebApplication.CreateBuilder(args);

builder.AddEnvVariablesAndConfigureSecrets();
builder.ConfigureKeycloakFromSettings();

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.AddServiceDefaults();
builder.Services.AddScoped<TagService>();
builder.Services.AddSingleton<IHtmlSanitizer, HtmlSanitizer>();
builder.Services.AddCommandFlow(typeof(Program).Assembly);
builder.AddKeyCloakAuthentication();

var connString = builder.Configuration.GetConnectionString("questionDb");

builder.Services.AddHealthChecks()
    .AddDatabaseHealthCheck<QuestionDbContext>()
    .AddRabbitMqHealthCheck();

builder.Services.AddDbContext<QuestionDbContext>(options => { options.UseNpgsql(connString); },
    optionsLifetime: ServiceLifetime.Singleton);

builder.AddFusionCacheWithRedis();

await builder.UseWolverineWithRabbitMqAsync(opts =>
{
    opts.ApplicationAssembly = typeof(Program).Assembly;
    opts.PersistMessagesWithPostgresql(connString!);
    opts.UseEntityFrameworkCoreTransactions();
    opts.PublishMessage<QuestionCreated>().ToRabbitExchange("Overflow.Contracts.QuestionCreated").UseDurableOutbox();
    opts.PublishMessage<QuestionUpdated>().ToRabbitExchange("Overflow.Contracts.QuestionUpdated").UseDurableOutbox();
    opts.PublishMessage<QuestionDeleted>().ToRabbitExchange("Overflow.Contracts.QuestionDeleted").UseDurableOutbox();
});

var app = builder.Build();

if (!app.Environment.IsProduction())
{
    app.MapOpenApi();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.MapDefaultEndpoints();

await app.MigrateDbContextAsync<QuestionDbContext>();
await TagSeeder.SeedDefaultTagsAsync(app);

app.Run();

namespace Overflow.QuestionService
{
    /// <summary>Marker type for WebApplicationFactory in integration tests.</summary>
    public partial class QuestionServiceMarker;
}