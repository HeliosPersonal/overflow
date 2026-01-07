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
builder.Services.AddMemoryCache();
builder.Services.AddScoped<TagService>();
builder.AddKeyCloakAuthentication();

var connString = builder.Configuration.GetConnectionString("questionDb");

builder.Services.AddHealthChecks()
    .AddDatabaseHealthCheck<QuestionDbContext>()
    .AddRabbitMqHealthCheck();

builder.Services.AddDbContext<QuestionDbContext>(options =>
{
    options.UseNpgsql(connString);
}, optionsLifetime: ServiceLifetime.Singleton);

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

// Configure the HTTP request pipeline.
if (!app.Environment.IsProduction())
{
    app.MapOpenApi();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapDefaultEndpoints();

await app.MigrateDbContextAsync<QuestionDbContext>();

app.Run();