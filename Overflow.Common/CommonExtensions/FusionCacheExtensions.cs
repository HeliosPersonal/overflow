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
using CacheOptions = Overflow.Common.Options.FusionCacheOptions;

namespace Overflow.Common.CommonExtensions;

public static class FusionCacheExtensions
{
    /// <summary>
    /// Registers FusionCache with L1 in-memory, L2 Redis, and Redis backplane.
    /// Cache keys are auto-prefixed with the environment name for isolation.
    /// </summary>
    public static IHostApplicationBuilder AddFusionCacheWithRedis(this IHostApplicationBuilder builder)
    {
        builder.Services
            .AddOptions<CacheOptions>()
            .BindConfiguration(CacheOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.AddSingleton<IValidateOptions<CacheOptions>>(sp =>
            new FusionCacheOptionsValidator(sp.GetRequiredService<IConfiguration>()));

        var options = builder.Services
            .BuildServiceProvider()
            .GetRequiredService<IOptions<CacheOptions>>().Value;

        var redisConnectionString = builder.Configuration.GetConnectionString(options.ConnectionStringName)!;

        // Environment prefix ensures staging and production never share cache keys
        // when they connect to the same Redis instance (single shared infra).
        var envPrefix = builder.Environment.EnvironmentName.ToLowerInvariant() + ":";

        builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var redisOptions = ConfigurationOptions.Parse(redisConnectionString);
            redisOptions.AbortOnConnectFail = false;
            return ConnectionMultiplexer.Connect(redisOptions);
        });

        builder.Services.AddFusionCache()
            .WithCacheKeyPrefix(envPrefix)
            .WithOptions(opts => { opts.BackplaneChannelPrefix = envPrefix; })
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
                            ConnectionMultiplexerFactory = () => Task.FromResult(redis),
                            InstanceName = envPrefix
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

internal sealed class FusionCacheOptionsValidator(IConfiguration configuration)
    : IValidateOptions<CacheOptions>
{
    public ValidateOptionsResult Validate(string? name, CacheOptions options)
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