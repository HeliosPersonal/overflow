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

            var endpoint = builder.Configuration.GetConnectionString("messaging");

            // Connection string may not be available yet in WebApplicationFactory tests
            // (test config is applied after Program.cs runs). Skip the probe — Wolverine
            // handles its own connection via UseRabbitMqUsingNamedConnection below.
            if (string.IsNullOrEmpty(endpoint))
            {
                logger.LogWarning("RabbitMQ connection string 'messaging' not found — skipping startup probe");
            }
            else
            {
                try
                {
                    var retryPolicy = Policy
                        .Handle<BrokerUnreachableException>()
                        .Or<SocketException>()
                        .WaitAndRetryAsync(
                            retryCount: 5,
                            retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                            (exception, timespan, retryCount, _) =>
                            {
                                logger.LogWarning(
                                    "RabbitMQ connection attempt {Attempt} failed, retrying in {Seconds}s: {Message}",
                                    retryCount, timespan.TotalSeconds, exception.Message);
                            });

                    await retryPolicy.ExecuteAsync(async () =>
                    {
                        var factory = new ConnectionFactory { Uri = new Uri(endpoint) };
                        await using var connection = await factory.CreateConnectionAsync();
                        logger.LogInformation("RabbitMQ connection established successfully");
                    });
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex,
                        "RabbitMQ startup probe failed — continuing anyway (Wolverine will manage its own connection)");
                }
            }
        }

        builder.Services
            .AddOpenTelemetry()
            .WithTracing(traceProviderBuilder =>
            {
                traceProviderBuilder.SetResourceBuilder(ResourceBuilder.CreateDefault()
                        .AddService(builder.Environment.ApplicationName))
                    .AddSource("Wolverine");
            });

        var messagingEndpoint = builder.Configuration.GetConnectionString("messaging");

        builder.UseWolverine(opts =>
        {
            // When the messaging connection string is unavailable (e.g. WebApplicationFactory tests
            // where env vars aren't set), stub all external transports so Wolverine doesn't
            // try to connect to RabbitMQ. Test fixtures should NOT set the messaging env var.
            if (string.IsNullOrEmpty(messagingEndpoint))
            {
                opts.StubAllExternalTransports();
            }
            else
            {
                opts.UseRabbitMqUsingNamedConnection("messaging")
                    .AutoProvision()
                    .UseConventionalRouting(x =>
                    {
                        x.QueueNameForListener(t => $"{t.FullName}.{builder.Environment.ApplicationName}");
                    });
            }

            configureMessaging(opts);
        });
    }
}