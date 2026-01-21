using Overflow.Common.CommonExtensions;
using Overflow.Common.Options;
using Overflow.DataSeederService.Models;
using Overflow.DataSeederService.Services;
using Overflow.ServiceDefaults;

var builder = Host.CreateApplicationBuilder(args);

builder.AddEnvVariablesAndConfigureSecrets();
builder.ConfigureKeycloakFromSettings();

// Configure options
builder.Services.Configure<SeederOptions>(builder.Configuration.GetSection("SeederOptions"));
builder.Services.Configure<KeycloakOptions>(builder.Configuration.GetSection("KeycloakOptions"));

// Add HttpClient with service discovery support
builder.Services.AddHttpClient<UserGenerator>();
builder.Services.AddHttpClient<QuestionGenerator>();
builder.Services.AddHttpClient<AnswerGenerator>();
builder.Services.AddHttpClient<VotingService>();
builder.Services.AddHttpClient<LlmClient>();
builder.Services.AddHttpClient<KeycloakAdminService>();

// Add services
builder.Services.AddSingleton<KeycloakAdminService>();
builder.Services.AddSingleton<AuthenticationService>();

// Add the background service
builder.Services.AddHostedService<SeederBackgroundService>();

builder.AddServiceDefaults();

var host = builder.Build();
host.Run();
