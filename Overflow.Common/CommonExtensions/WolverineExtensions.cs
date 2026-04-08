using System.Net.Sockets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using Wolverine;
using Wolverine.RabbitMQ;

namespace Overflow.Common.CommonExtensions;

public static class WolverineExtensions
{
    public static async Task UseWolverineWithRabbitMqAsync(
        this IHostApplicationBuilder builder,
        Action<WolverineOptions> configureMessaging,
        int? maximumParallelMessages = null)
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

        // Add the Wolverine activity source to the existing TracerProvider that was
        // already configured (with the correct resource) by AddServiceDefaults().
        // Do NOT call SetResourceBuilder here — it would create a second resource
        // configuration that conflicts with the one in Extensions.cs and produces
        // duplicate deployment.environment values in the Aspire dashboard.
        builder.Services
            .AddOpenTelemetry()
            .WithTracing(traceProviderBuilder =>
            {
                traceProviderBuilder.AddSource("Wolverine");
            });

        builder.UseWolverine(opts =>
        {
            var transport = opts.UseRabbitMqUsingNamedConnection("messaging")
                .AutoProvision();

            transport.UseConventionalRouting(x =>
            {
                x.QueueNameForListener(t => $"{t.FullName}.{builder.Environment.ApplicationName}");
            });

            if (maximumParallelMessages.HasValue)
                transport.ConfigureListeners(l => l.MaximumParallelMessages(maximumParallelMessages.Value));

            configureMessaging(opts);
        });
    }
}