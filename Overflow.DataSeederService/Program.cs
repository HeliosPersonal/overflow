using Microsoft.Extensions.Options;
using OllamaSharp;
using Overflow.Common.CommonExtensions;
using Overflow.Common.Options;
using Overflow.DataSeederService;
using Overflow.DataSeederService.Clients;
using Overflow.DataSeederService.Keycloak;
using Overflow.DataSeederService.Models;
using Overflow.DataSeederService.Extensions;
using Overflow.DataSeederService.Services;
using Overflow.ServiceDefaults;
using Refit;

var builder = WebApplication.CreateBuilder(args);

builder.AddEnvVariablesAndConfigureSecrets();
builder.ConfigureKeycloakFromSettings();

// ── Options with validation ──────────────────────────────────────────────
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

// OllamaSharp client — use a custom HttpClient with extended timeout for LLM inference
builder.Services.AddSingleton<IOllamaApiClient>(sp =>
{
    var options = sp.GetRequiredService<IOptions<AiAnswerOptions>>().Value;
    var httpClient = new HttpClient
    {
        BaseAddress = new Uri(options.LlmApiUrl),
        Timeout = TimeSpan.FromSeconds(options.LlmTimeoutSeconds)
    };
    return new OllamaApiClient(httpClient, options.LlmModel);
});

builder.AddServiceDefaults();

// Health checks
builder.Services.AddHealthChecks()
    .AddRabbitMqHealthCheck();

// Wolverine + RabbitMQ for consuming QuestionCreated events
// Wolverine automatically retries failed messages (3 attempts by default) and moves to DLQ on permanent failure
await builder.UseWolverineWithRabbitMqAsync(opts => { opts.ApplicationAssembly = typeof(Program).Assembly; });

// ── Refit HTTP clients ─────────────────────────────────────────────────
// The global AddStandardResilienceHandler (30s total / 10s attempt) from ServiceDefaults
// is too aggressive for cross-namespace K8s calls. Override with extended timeouts.

builder.Services.AddTransient<AdminBearerTokenHandler>();

builder.Services.AddRefitClient<IKeycloakTokenClient>()
    .ConfigureHttpClient((sp, c) =>
    {
        var keycloakOptions = sp.GetRequiredService<IOptions<KeycloakOptions>>().Value;
        c.BaseAddress = new Uri($"{keycloakOptions.Url}/realms/{keycloakOptions.Realm}");
        c.Timeout = TimeSpan.FromSeconds(60);
    })
    .AddDataSeederResilienceHandler();

builder.Services.AddRefitClient<IKeycloakAdminClient>()
    .ConfigureHttpClient((sp, c) =>
    {
        var keycloakOptions = sp.GetRequiredService<IOptions<KeycloakOptions>>().Value;
        c.BaseAddress = new Uri($"{keycloakOptions.Url}/admin/realms/{keycloakOptions.Realm}");
        c.Timeout = TimeSpan.FromSeconds(60);
    })
    .AddHttpMessageHandler<AdminBearerTokenHandler>()
    .AddDataSeederResilienceHandler();

builder.Services.AddRefitClient<IQuestionApiClient>()
    .ConfigureHttpClient((sp, c) =>
    {
        var aiOptions = sp.GetRequiredService<IOptions<AiAnswerOptions>>().Value;
        c.BaseAddress = new Uri(aiOptions.QuestionServiceUrl);
        c.Timeout = TimeSpan.FromSeconds(60);
    })
    .AddDataSeederResilienceHandler();

builder.Services.AddRefitClient<IProfileApiClient>()
    .ConfigureHttpClient((sp, c) =>
    {
        var aiOptions = sp.GetRequiredService<IOptions<AiAnswerOptions>>().Value;
        c.BaseAddress = new Uri(aiOptions.ProfileServiceUrl);
        c.Timeout = TimeSpan.FromSeconds(60);
    })
    .AddDataSeederResilienceHandler();

// Services
builder.Services.AddSingleton<KeycloakAdminService>();
builder.Services.AddSingleton<AiUserProvider>();
builder.Services.AddSingleton<LlmService>();
builder.Services.AddScoped<AiAnswerService>();

// Bootstrap AI user on startup
builder.Services.AddHostedService<AiUserBootstrapService>();

var app = builder.Build();

app.MapDefaultEndpoints();

var logger = app.Services.GetRequiredService<ILogger<Program>>();
var options = app.Services.GetRequiredService<IOptions<AiAnswerOptions>>().Value;

logger.LogInformation(
    "AI Answer Service starting — AI User: '{DisplayName}' | LLM: {Model} | Variants: {Variants}",
    options.AiDisplayName, options.LlmModel, options.AnswerVariants);

app.Run();


