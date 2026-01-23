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

// LLM client needs custom longer timeouts - configure BEFORE AddServiceDefaults
// to avoid default 30s timeout being applied by ConfigureHttpClientDefaults
builder.Services.AddHttpClient<LlmClient>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(5);
})
.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
{
    PooledConnectionLifetime = TimeSpan.FromMinutes(15),
    PooledConnectionIdleTimeout = TimeSpan.FromMinutes(5),
    ConnectTimeout = TimeSpan.FromSeconds(30)
})
.AddStandardResilienceHandler(options =>
{
    // Configure timeout for LLM requests (should be less than client timeout)
    options.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(4);
    options.AttemptTimeout.Timeout = TimeSpan.FromMinutes(2);
    
    // Retry with exponential backoff
    options.Retry.MaxRetryAttempts = 2;
    options.Retry.UseJitter = true;
    options.Retry.BackoffType = Polly.DelayBackoffType.Exponential;
    
    // Circuit breaker - more lenient for LLM
    // Sampling duration must be at least double the attempt timeout (2 min * 2 = 4 min)
    options.CircuitBreaker.SamplingDuration = TimeSpan.FromMinutes(5);
    options.CircuitBreaker.FailureRatio = 0.5;
    options.CircuitBreaker.MinimumThroughput = 3;
});

// NOW add ServiceDefaults which will configure default HttpClient settings for other clients
// Service discovery will be added to ALL HttpClients including LlmClient
builder.AddServiceDefaults();

// Add HttpClient with service discovery support (uses default resilience settings)
builder.Services.AddHttpClient<UserGenerator>();
builder.Services.AddHttpClient<QuestionGenerator>();
builder.Services.AddHttpClient<AnswerGenerator>();
builder.Services.AddHttpClient<VotingService>();
builder.Services.AddHttpClient<KeycloakAdminService>();

// Add services
builder.Services.AddSingleton<KeycloakAdminService>();
builder.Services.AddSingleton<AuthenticationService>();

// Add the background service
builder.Services.AddHostedService<SeederBackgroundService>();


var host = builder.Build();
host.Run();
