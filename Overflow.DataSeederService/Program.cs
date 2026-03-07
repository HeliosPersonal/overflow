using Microsoft.Extensions.Options;
using Overflow.Common.CommonExtensions;
using Overflow.Common.Options;
using Overflow.DataSeederService.Models;
using Overflow.DataSeederService.Services;

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
    // Set infinite timeout - let Polly resilience handler manage timeouts
    client.Timeout = Timeout.InfiniteTimeSpan;
})
.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
{
    PooledConnectionLifetime = TimeSpan.FromMinutes(15),
    PooledConnectionIdleTimeout = TimeSpan.FromMinutes(5),
    ConnectTimeout = TimeSpan.FromSeconds(30)
})
.AddStandardResilienceHandler(options =>
{
    // Configure timeout for LLM requests - VERY generous timeouts for model loading and generation
    // Answer generation can take 3-5 minutes depending on complexity
    options.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(10);
    options.AttemptTimeout.Timeout = TimeSpan.FromMinutes(8);
    
    // Retry - only on network failures, not timeouts
    options.Retry.MaxRetryAttempts = 1;
    options.Retry.UseJitter = true;
    options.Retry.BackoffType = Polly.DelayBackoffType.Exponential;
    
    // Disable circuit breaker for LLM - model loading can take unpredictable time
    options.CircuitBreaker.SamplingDuration = TimeSpan.FromMinutes(20);
    options.CircuitBreaker.FailureRatio = 0.9; // Very lenient
    options.CircuitBreaker.MinimumThroughput = 100; // Effectively disabled
});

// Add ServiceDefaults AFTER LlmClient so ConfigureHttpClientDefaults doesn't affect it
// Note: ConfigureHttpClientDefaults applies to clients added AFTER it's called
//builder.AddServiceDefaults();

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

// Log configuration on startup
var logger = host.Services.GetRequiredService<ILogger<Program>>();
var seederOptions = host.Services.GetRequiredService<IOptions<SeederOptions>>().Value;
logger.LogInformation("=== Data Seeder Configuration ===");
logger.LogInformation("LLM API URL: {Url}", seederOptions.LlmApiUrl);
logger.LogInformation("LLM Model: {Model}", seederOptions.LlmModel);
logger.LogInformation("LLM Generation Enabled: {Enabled}", seederOptions.EnableLlmGeneration);
logger.LogInformation("Interval: {Minutes} minutes", seederOptions.IntervalMinutes);
logger.LogInformation("Question Service: {Url}", seederOptions.QuestionServiceUrl);
logger.LogInformation("Profile Service: {Url}", seederOptions.ProfileServiceUrl);
logger.LogInformation("Vote Service: {Url}", seederOptions.VoteServiceUrl);
logger.LogInformation("==================================");

host.Run();
