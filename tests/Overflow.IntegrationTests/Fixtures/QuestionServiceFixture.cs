using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Overflow.IntegrationTests.Auth;
using Overflow.QuestionService;
using Overflow.QuestionService.Data;
using Testcontainers.PostgreSql;
using ZiggyCreatures.Caching.Fusion;
using Tag = Overflow.QuestionService.Models.Tag;

namespace Overflow.IntegrationTests.Fixtures;

public class QuestionServiceFixture : WebApplicationFactory<QuestionServiceMarker>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .WithDatabase("question_test")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();

        // Set connection strings as env vars so they're visible to Program.cs at startup.
        // WebApplicationFactory's ConfigureAppConfiguration runs too late — Program.cs has
        // already captured connection strings before those callbacks fire.
        // Note: messaging is NOT set here — the RabbitMQ probe will skip, and Wolverine
        // transports are stubbed via StubAllExternalTransports() in ConfigureTestServices.
        Environment.SetEnvironmentVariable("ConnectionStrings__questionDb", _postgres.GetConnectionString());
        Environment.SetEnvironmentVariable("ConnectionStrings__question-redis", "localhost:6379");
    }

    public override async ValueTask DisposeAsync()
    {
        // Clean up env vars
        Environment.SetEnvironmentVariable("ConnectionStrings__questionDb", null);
        Environment.SetEnvironmentVariable("ConnectionStrings__question-redis", null);

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
                ["ConnectionStrings:messaging"] = "amqp://guest:guest@localhost:5672",
                ["ConnectionStrings:questionDb"] = _postgres.GetConnectionString(),
                ["ConnectionStrings:question-redis"] = "localhost:6379",
                ["FusionCache:ConnectionStringName"] = "question-redis",
                ["FusionCache:DurationSeconds"] = "120",
                ["FusionCache:IsFailSafeEnabled"] = "true",
                ["FusionCache:FailSafeMaxDurationSeconds"] = "600",
                ["FusionCache:FailSafeThrottleDurationSeconds"] = "10",
                ["KeycloakOptions:Url"] = "http://localhost:6001",
                ["KeycloakOptions:ServiceName"] = "overflow-service",
                ["KeycloakOptions:Realm"] = "overflow",
                ["KeycloakOptions:Audience"] = "overflow",
                ["KeycloakOptions:ValidIssuers:0"] = "http://localhost:6001/realms/overflow",
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<QuestionDbContext>>();
            services.RemoveAll<QuestionDbContext>();
            services.AddDbContext<QuestionDbContext>(opts =>
                opts.UseNpgsql(_postgres.GetConnectionString()));

            services.RemoveAll<IFusionCache>();
            services.AddFusionCache();

            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.SchemeName, _ => { });
        });
    }

    public HttpClient CreateAuthenticatedClient(string userId = "test-user-1", params string[] roles)
    {
        var client = CreateClient();
        client.SetTestUser(userId, roles);
        return client;
    }

    public async Task SeedTagsAsync(params (string slug, string name)[] tags)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QuestionDbContext>();
        await db.Database.EnsureCreatedAsync();
        foreach (var (slug, name) in tags)
        {
            if (!await db.Tags.AnyAsync(t => t.Slug == slug))
                db.Tags.Add(new Tag { Id = slug, Name = name, Slug = slug, Description = $"{name} tag" });
        }

        await db.SaveChangesAsync();
    }

    public async Task EnsureDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QuestionDbContext>();
        await db.Database.EnsureCreatedAsync();
    }
}