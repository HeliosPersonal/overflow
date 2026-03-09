using Microsoft.Extensions.Options;
using OllamaSharp;
using Overflow.Common.CommonExtensions;
using Overflow.Common.Options;
using Overflow.DataSeederService.Clients;
using Overflow.DataSeederService.Jobs;
using Overflow.DataSeederService.Keycloak;
using Overflow.DataSeederService.Models;
using Overflow.DataSeederService.Services;
using Overflow.ServiceDefaults;
using Refit;

var builder = Host.CreateApplicationBuilder(args);

builder.AddEnvVariablesAndConfigureSecrets();
builder.ConfigureKeycloakFromSettings();

var seederOptions = builder.Configuration.GetSection("SeederOptions").Get<SeederOptions>()
    ?? throw new InvalidOperationException("SeederOptions section is missing");
var keycloakOptions = builder.Configuration.GetSection("KeycloakOptions").Get<KeycloakOptions>()
    ?? throw new InvalidOperationException("KeycloakOptions section is missing");

builder.Services.Configure<SeederOptions>(builder.Configuration.GetSection("SeederOptions"));
builder.Services.Configure<KeycloakOptions>(builder.Configuration.GetSection("KeycloakOptions"));

// OllamaSharp client — long timeout for slow LLM responses
builder.Services.AddSingleton<IOllamaApiClient>(_ =>
    new OllamaApiClient(new Uri(seederOptions.LlmApiUrl), seederOptions.LlmModel));

builder.AddServiceDefaults();

// Keycloak
builder.Services.AddTransient<AdminBearerTokenHandler>();

builder.Services.AddRefitClient<IKeycloakTokenClient>()
    .ConfigureHttpClient(c =>
        c.BaseAddress = new Uri($"{keycloakOptions.Url}/realms/{keycloakOptions.Realm}"));

builder.Services.AddRefitClient<IKeycloakAdminClient>()
    .ConfigureHttpClient(c =>
        c.BaseAddress = new Uri($"{keycloakOptions.Url}/admin/realms/{keycloakOptions.Realm}"))
    .AddHttpMessageHandler<AdminBearerTokenHandler>();

// Domain API clients
builder.Services.AddRefitClient<IQuestionApiClient>()
    .ConfigureHttpClient(c => c.BaseAddress = new Uri(seederOptions.QuestionServiceUrl));

builder.Services.AddRefitClient<IProfileApiClient>()
    .ConfigureHttpClient(c => c.BaseAddress = new Uri(seederOptions.ProfileServiceUrl));

builder.Services.AddRefitClient<IVoteApiClient>()
    .ConfigureHttpClient(c => c.BaseAddress = new Uri(seederOptions.VoteServiceUrl));

// Services
builder.Services.AddSingleton<KeycloakAdminService>();
builder.Services.AddSingleton<SeederUserService>();
builder.Services.AddSingleton<UserSyncService>();
builder.Services.AddSingleton<SeederUserPool>();
builder.Services.AddSingleton<LlmService>();
builder.Services.AddSingleton<QuestionService>();
builder.Services.AddSingleton<AnswerService>();
builder.Services.AddSingleton<VotingService>();

// Jobs
builder.Services.AddHostedService<PostQuestionJob>();
builder.Services.AddHostedService<PostAnswerJob>();
builder.Services.AddHostedService<AcceptBestAnswerJob>();

var host = builder.Build();

var logger = host.Services.GetRequiredService<ILogger<Program>>();
var options = host.Services.GetRequiredService<IOptions<SeederOptions>>().Value;

logger.LogInformation(
    "Data Seeder starting — Question every {Q}min, Answer every {A}min, Accept every {Acc}min | LLM: {Model}",
    options.QuestionIntervalMinutes, options.AnswerIntervalMinutes, options.AcceptIntervalMinutes,
    options.LlmModel);

host.Run();