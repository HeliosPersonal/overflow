using System.Net.Sockets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Polly;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using Wolverine;
using Wolverine.RabbitMQ;

namespace Overflow.Common.CommonExtensions;

public static class WolverineExtensions
{
    public static async Task UseWolverineWithRabbitMqAsync(
        this IHostApplicationBuilder builder, Action<WolverineOptions> configureMessaging)
    {
        var isEfDesignTime = AppDomain.CurrentDomain.FriendlyName.StartsWith("ef", StringComparison.OrdinalIgnoreCase);

        if (!isEfDesignTime)
        {
            using var loggerFactory = LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Information));
            var logger = loggerFactory.CreateLogger("RabbitMQConnection");
            
            var retryPolicy = Policy
                .Handle<BrokerUnreachableException>()
                .Or<SocketException>()
                .WaitAndRetryAsync(
                    retryCount: 5,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    (exception, timespan, retryCount, _) =>
                    {
                        logger.LogWarning("RabbitMQ connection attempt {Attempt} failed, retrying in {Seconds}s: {Message}", 
                            retryCount, timespan.TotalSeconds, exception.Message);
                    });

            await retryPolicy.ExecuteAsync(async () =>
            {
                var endpoint = builder.Configuration.GetConnectionString("messaging") ??
                               throw new InvalidOperationException("cannot get messaging connection string");

                var factory = new ConnectionFactory
                {
                    Uri = new Uri(endpoint)
                };
                await using var connection = await factory.CreateConnectionAsync();
                logger.LogInformation("RabbitMQ connection established successfully");
            });
        }

        builder.Services
            .AddOpenTelemetry()
            .WithTracing(traceProviderBuilder =>
            {
                traceProviderBuilder.SetResourceBuilder(ResourceBuilder.CreateDefault()
                        .AddService(builder.Environment.ApplicationName))
                    .AddSource("Wolverine");
            });

        builder.UseWolverine(opts =>
        {
            opts.UseRabbitMqUsingNamedConnection("messaging")
                .AutoProvision()
                .UseConventionalRouting(x =>
                {
                    x.QueueNameForListener(t => $"{t.FullName}.{builder.Environment.ApplicationName}");
                });

            configureMessaging(opts);
        });
    }
}