using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Overflow.Common.Options;
using StackExchange.Redis;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Backplane.StackExchangeRedis;
using ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;

namespace Overflow.Common.CommonExtensions;

public static class FusionCacheExtensions
{
    /// <summary>
    /// Registers Redis (<see cref="IConnectionMultiplexer"/>) and FusionCache
    /// (L1 in-memory + L2 Redis + backplane for cross-pod invalidation).
    /// Configuration is read from the <c>FusionCache</c> section of appsettings
    /// and validated at startup via <see cref="IOptions{TOptions}"/>.
    /// </summary>
    public static IHostApplicationBuilder AddFusionCacheWithRedis(this IHostApplicationBuilder builder)
    {
        builder.Services
            .AddOptions<DistributedCacheOptions>()
            .BindConfiguration(DistributedCacheOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.AddSingleton<IValidateOptions<DistributedCacheOptions>>(sp =>
            new DistributedCacheOptionsValidator(sp.GetRequiredService<IConfiguration>()));

        // Resolve validated options eagerly so Redis + FusionCache can be configured inline.
        // Same pattern used in AuthExtensions for KeycloakOptions.
        var options = builder.Services
            .BuildServiceProvider()
            .GetRequiredService<IOptions<DistributedCacheOptions>>().Value;

        var redisConnectionString = builder.Configuration.GetConnectionString(options.ConnectionStringName)!;

        builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var redisOptions = ConfigurationOptions.Parse(redisConnectionString);
            redisOptions.AbortOnConnectFail = false;
            return ConnectionMultiplexer.Connect(redisOptions);
        });

        builder.Services.AddFusionCache()
            .WithDefaultEntryOptions(new FusionCacheEntryOptions
            {
                Duration = TimeSpan.FromSeconds(options.DurationSeconds),
                IsFailSafeEnabled = options.IsFailSafeEnabled,
                FailSafeMaxDuration = TimeSpan.FromSeconds(options.FailSafeMaxDurationSeconds),
                FailSafeThrottleDuration = TimeSpan.FromSeconds(options.FailSafeThrottleDurationSeconds),
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

        return builder;
    }
}

internal sealed class DistributedCacheOptionsValidator(IConfiguration configuration)
    : IValidateOptions<DistributedCacheOptions>
{
    public ValidateOptionsResult Validate(string? name, DistributedCacheOptions options)
    {
        var connectionString = configuration.GetConnectionString(options.ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return ValidateOptionsResult.Fail(
                $"Connection string '{options.ConnectionStringName}' is missing. " +
                $"Ensure it is configured in ConnectionStrings or injected by Aspire.");
        }

        return ValidateOptionsResult.Success;
    }
}