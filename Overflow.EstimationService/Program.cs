using System.Text.Json;
using Overflow.Common.CommonExtensions;
using Overflow.EstimationService.Auth;
using Overflow.EstimationService.Clients;
using Overflow.EstimationService.Data;
using Overflow.EstimationService.Extensions;
using Overflow.EstimationService.Options;
using Overflow.EstimationService.Services;
using Overflow.ServiceDefaults;
using StackExchange.Redis;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Backplane.StackExchangeRedis;
using ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;

var builder = WebApplication.CreateBuilder(args);

builder.AddEnvVariablesAndConfigureSecrets();
builder.ConfigureKeycloakFromSettings();

builder.Services.AddOpenApi();
builder.AddServiceDefaults();
builder.AddKeyCloakAuthentication();

builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        opts.JsonSerializerOptions.DefaultIgnoreCondition =
            System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });

// ── EF Core + PostgreSQL ─────────────────────────────────────────────────
builder.AddNpgsqlDbContext<EstimationDbContext>("estimationDb");

// ── Redis ────────────────────────────────────────────────────────────────
// Dev: Aspire injects "ConnectionStrings:estimation-redis" automatically
// Staging/Prod: Infisical provides "ConnectionStrings__Redis" (maps to "ConnectionStrings:Redis")
var redisConnectionString = builder.Configuration.GetConnectionString("estimation-redis")
                            ?? builder.Configuration.GetConnectionString("Redis")
                            ?? "localhost:6379";

builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
{
    var options = ConfigurationOptions.Parse(redisConnectionString);
    options.AbortOnConnectFail = false; // Don't block startup if Redis is temporarily unreachable
    return ConnectionMultiplexer.Connect(options);
});

// ── FusionCache (L1 in-memory + L2 Redis + backplane for cross-pod invalidation) ──
builder.Services.AddFusionCache()
    .WithDefaultEntryOptions(new FusionCacheEntryOptions
    {
        Duration = TimeSpan.FromSeconds(30),
        IsFailSafeEnabled = true,
        FailSafeMaxDuration = TimeSpan.FromMinutes(5),
        FailSafeThrottleDuration = TimeSpan.FromSeconds(5),
    })
    .WithSerializer(new FusionCacheSystemTextJsonSerializer(new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles,
    }))
    .WithDistributedCache(sp =>
    {
        var redis = sp.GetRequiredService<IConnectionMultiplexer>();
        return new Microsoft.Extensions.Caching.StackExchangeRedis.RedisCache(
            Microsoft.Extensions.Options.Options.Create(
                new Microsoft.Extensions.Caching.StackExchangeRedis.RedisCacheOptions
                {
                    ConnectionMultiplexerFactory = () => Task.FromResult(redis)
                }));
    })
    .WithBackplane(sp =>
    {
        var redis = sp.GetRequiredService<IConnectionMultiplexer>();
        return new RedisBackplane(
            new RedisBackplaneOptions
            {
                ConnectionMultiplexerFactory = () => Task.FromResult(redis)
            });
    });

// ── Services ─────────────────────────────────────────────────────────────
builder.Services.AddScoped<EstimationRoomService>();
builder.Services.AddSingleton<WebSocketBroadcaster>();
builder.Services.AddSingleton<RoomCacheService>();
builder.Services.AddSingleton<CrossPodBroadcastService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<CrossPodBroadcastService>());

// ── Profile Service (display name resolution) ───────────────────────────
builder.Services.AddHttpClient<ProfileServiceClient>(client =>
{
    var profileUrl = builder.Configuration["PROFILE_SERVICE_URL"]
                     ?? builder.Configuration["services:profile-svc:https:0"]
                     ?? builder.Configuration["services:profile-svc:http:0"]
                     ?? "http://localhost:8001";
    client.BaseAddress = new Uri(profileUrl);
    client.Timeout = TimeSpan.FromSeconds(5);
});
builder.Services.AddScoped<IdentityResolver>();

// ── Archived room cleanup ────────────────────────────────────────────────
builder.Services.Configure<RoomCleanupOptions>(
    builder.Configuration.GetSection(RoomCleanupOptions.SectionName));
builder.Services.AddHostedService<ArchivedRoomCleanupService>();

builder.Services.AddHealthChecks()
    .AddDatabaseHealthCheck<EstimationDbContext>();

var app = builder.Build();

if (!app.Environment.IsProduction())
{
    app.MapOpenApi();
}

app.UseAuthentication();
app.UseAuthorization();
app.UseWebSockets(
    new WebSocketOptions
    {
        KeepAliveInterval = TimeSpan.FromSeconds(30)
    });

app.MapControllers();
app.MapEstimationWebSocket();
app.MapDefaultEndpoints();

await app.MigrateDbContextAsync<EstimationDbContext>();

app.Run();