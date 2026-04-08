using Microsoft.Extensions.Options;
using Overflow.Common.CommonExtensions;
using Overflow.Common.Options;
using Overflow.DataSeederService.Extensions;
using Overflow.DataSeederService.Models;
using Overflow.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddEnvVariablesAndConfigureSecrets();
builder.ConfigureKeycloakFromSettings();

builder.Services
    .AddOptions<AiAnswerOptions>()
    .BindConfiguration(AiAnswerOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<KeycloakOptions>()
    .BindConfiguration(KeycloakOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOllamaClient()
    .AddKeycloakClients()
    .AddBackendApiClients()
    .AddApplicationServices();

builder.AddServiceDefaults();

builder.Services.AddHealthChecks()
    .AddRabbitMqHealthCheck();

await builder
    .UseWolverineWithRabbitMqAsync(
        opts => { opts.ApplicationAssembly = typeof(Program).Assembly; },
        maximumParallelMessages: 1);

var app = builder.Build();

app.MapDefaultEndpoints();

var logger = app.Services.GetRequiredService<ILogger<Program>>();
var opts = app.Services.GetRequiredService<IOptions<AiAnswerOptions>>().Value;

logger.LogInformation(
    "DataSeederService starting — User: {Email} | LLM: {Model} @ {Url} | Variants: {V}",
    string.IsNullOrWhiteSpace(opts.AiEmail) ? "(NOT SET)" : opts.AiEmail,
    opts.LlmModel, opts.LlmApiUrl, opts.AnswerVariants);

if (string.IsNullOrWhiteSpace(opts.AiEmail) || string.IsNullOrWhiteSpace(opts.AiPassword))
{
    logger.LogWarning("AiEmail or AiPassword not configured — AI answers disabled");
}

app.Run();