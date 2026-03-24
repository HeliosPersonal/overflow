using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Overflow.EstimationService;
using Overflow.EstimationService.Data;
using Overflow.EstimationService.Services;
using Overflow.IntegrationTests.Auth;
using StackExchange.Redis;
using Testcontainers.PostgreSql;
using ZiggyCreatures.Caching.Fusion;

namespace Overflow.IntegrationTests.Fixtures;

public class EstimationServiceFixture : WebApplicationFactory<EstimationServiceMarker>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .WithDatabase("estimation_test")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    public new async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();
    }

    public override async ValueTask DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:estimationDb"] = _postgres.GetConnectionString(),
                ["ConnectionStrings:estimation-redis"] = "localhost:6379",
                ["FusionCache:ConnectionStringName"] = "estimation-redis",
                ["FusionCache:DurationSeconds"] = "30",
                ["FusionCache:IsFailSafeEnabled"] = "true",
                ["FusionCache:FailSafeMaxDurationSeconds"] = "300",
                ["FusionCache:FailSafeThrottleDurationSeconds"] = "5",
                ["RoomCleanup:RetentionDays"] = "10",
                ["RoomCleanup:IntervalHours"] = "24",
                ["APP_BASE_URL"] = "http://localhost:3000",
                ["KeycloakOptions:Url"] = "http://localhost:6001",
                ["KeycloakOptions:ServiceName"] = "overflow-service",
                ["KeycloakOptions:Realm"] = "overflow",
                ["KeycloakOptions:Audience"] = "overflow",
                ["KeycloakOptions:ValidIssuers:0"] = "http://localhost:6001/realms/overflow",
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // ── EF Core: fully replace Aspire's pooled DbContext with plain scoped DbContext ──
            // Aspire's AddNpgsqlDbContext registers many internal descriptors (pool, lease,
            // options factory, configuration). Remove every descriptor that references
            // EstimationDbContext in its service or implementation type.
            static bool ReferencesEstimationDb(Type? type)
                => type is not null && (type == typeof(EstimationDbContext)
                                        || type.IsGenericType &&
                                        type.GenericTypeArguments.Any(a => a == typeof(EstimationDbContext)));

            var toRemove = services
                .Where(d => ReferencesEstimationDb(d.ServiceType) || ReferencesEstimationDb(d.ImplementationType))
                .ToList();
            foreach (var descriptor in toRemove)
                services.Remove(descriptor);

            services.AddDbContext<EstimationDbContext>(opts =>
                opts.UseNpgsql(_postgres.GetConnectionString()));

            // ── FusionCache: replace with in-memory only (no Redis) ──
            services.RemoveAll<IFusionCache>();
            services.RemoveAll<IConnectionMultiplexer>();
            services.AddFusionCache();

            // ── CrossPodBroadcastService: replace with no-op stub (no Redis needed) ──
            services.RemoveAll<CrossPodBroadcastService>();
            services.AddSingleton<CrossPodBroadcastService>(sp =>
                new NullCrossPodBroadcastService(
                    sp.GetRequiredService<WebSocketBroadcaster>(),
                    sp.GetRequiredService<IHostEnvironment>(),
                    sp.GetRequiredService<ILoggerFactory>().CreateLogger<CrossPodBroadcastService>()));

            // ── Replace authentication with test scheme ──
            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.SchemeName, _ => { });

            // ── Remove background hosted services that need Redis/timers ──
            services.RemoveAll<ArchivedRoomCleanupService>();
            var hostedDescriptors = services
                .Where(d => d.ServiceType == typeof(IHostedService))
                .ToList();
            foreach (var descriptor in hostedDescriptors)
                services.Remove(descriptor);
        });
    }

    public HttpClient CreateAuthenticatedClient(string userId = "test-user-1", params string[] roles)
    {
        var client = CreateClient();
        client.SetTestUser(userId, roles);
        return client;
    }

    /// <summary>
    /// Creates a guest client with a stable guest cookie.
    /// </summary>
    public HttpClient CreateGuestClient(string guestId = "guest_test123")
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true
        });
        // Set the guest cookie on the request
        client.DefaultRequestHeaders.Add("Cookie", $"overflow_guest_id={guestId}");
        return client;
    }

    public async Task EnsureDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EstimationDbContext>();
        await db.Database.EnsureCreatedAsync();
    }
}

/// <summary>
/// No-op replacement for <see cref="CrossPodBroadcastService"/> in integration tests.
/// Avoids needing a real Redis connection. PublishRoomUpdateAsync is a no-op.
/// </summary>
file class NullCrossPodBroadcastService(
    WebSocketBroadcaster broadcaster,
    IHostEnvironment environment,
    ILogger<CrossPodBroadcastService> logger)
    : CrossPodBroadcastService(null!, broadcaster, environment, logger)
{
    public override Task PublishRoomUpdateAsync(Guid roomId) => Task.CompletedTask;
}