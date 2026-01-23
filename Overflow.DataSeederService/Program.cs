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

// Manually configure service discovery (needed for all HttpClients)
builder.Services.AddServiceDiscovery();

// LLM client needs custom longer timeouts - configure WITHOUT AddServiceDefaults interference
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
    
    // Circuit breaker - more lenient for LLM (for cold start and model loading)
    // Sampling duration must be at least double the attempt timeout (2 min * 2 = 4 min)
    options.CircuitBreaker.SamplingDuration = TimeSpan.FromMinutes(5);
    options.CircuitBreaker.FailureRatio = 0.5;
    options.CircuitBreaker.MinimumThroughput = 3;
});

// Add ServiceDefaults AFTER LlmClient so ConfigureHttpClientDefaults doesn't affect it
// Note: ConfigureHttpClientDefaults applies to clients added AFTER it's called
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
