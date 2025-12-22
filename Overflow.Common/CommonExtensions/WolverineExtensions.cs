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
    /// <summary>
    /// Configures Wolverine with RabbitMQ messaging using a fluent builder pattern.
    /// This method synchronously configures Wolverine and asynchronously tests the RabbitMQ connection.
    /// Wolverine will automatically use the virtual host specified in the connection string.
    /// </summary>
    public static IHostApplicationBuilder UseWolverineWithRabbitMqAsync(
        this IHostApplicationBuilder builder, Action<WolverineOptions> configureMessaging)
    {
        var ifEfDesignTime = AppDomain.CurrentDomain.FriendlyName.StartsWith("ef", StringComparison.OrdinalIgnoreCase);
        if (ifEfDesignTime)
        {
            return builder;
        }

        var logger = CreateLogger(builder);

        // Get connection string and log configuration details
        var endpoint = builder.Configuration.GetConnectionString("messaging");
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            logger.LogError(
                "RabbitMQ connection string 'messaging' not found. Expected ConnectionStrings__messaging environment variable");
            throw new InvalidOperationException(
                "RabbitMQ connection string 'messaging' not found. Expected ConnectionStrings__messaging environment variable.");
        }

        var connectionUri = new Uri(endpoint);
        var vhost = connectionUri.AbsolutePath.TrimStart('/');
        var username = connectionUri.UserInfo.Split(':')[0];

        logger.LogInformation("🐰 Configuring Wolverine with RabbitMQ");
        logger.LogInformation("   Host: {Host}:{Port}", connectionUri.Host, connectionUri.Port);
        logger.LogInformation("   Virtual Host: {VirtualHost}", string.IsNullOrEmpty(vhost) ? "/" : $"/{vhost}");
        logger.LogInformation("   User: {Username}", username);

        // Validate RabbitMQ connection asynchronously in the background
        _ = Task.Run(async () => await ValidateRabbitMqConnectionAsync(builder));

        // Configure OpenTelemetry
        builder.Services
            .AddOpenTelemetry()
            .WithTracing(traceProviderBuilder =>
            {
                traceProviderBuilder.SetResourceBuilder(ResourceBuilder.CreateDefault()
                        .AddService(builder.Environment.ApplicationName))
                    .AddSource("Wolverine");
            });

        // Configure Wolverine - it automatically reads virtual host from connection string
        builder.UseWolverine(opts =>
        {
            opts
                .UseRabbitMqUsingNamedConnection("messaging")
                .AutoProvision()
                .DeclareExchange("questions");

            configureMessaging(opts);
        });

        return builder;
    }

    private static async Task ValidateRabbitMqConnectionAsync(IHostApplicationBuilder builder)
    {
        var logger = CreateLogger(builder);


        var retryPolicy = Policy
            .Handle<BrokerUnreachableException>()
            .Or<SocketException>()
            .WaitAndRetryAsync(
                retryCount: 5,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                (exception, timeSpan, retryCount) =>
                {
                    logger.LogWarning(exception,
                        "RabbitMQ connection retry attempt {RetryCount} failed. Retrying in {RetryDelay} seconds",
                        retryCount, timeSpan.TotalSeconds);
                });

        await retryPolicy.ExecuteAsync(async () =>
        {
            var endpoint = builder.Configuration.GetConnectionString("messaging");

            if (string.IsNullOrWhiteSpace(endpoint))
            {
                logger.LogError(
                    "RabbitMQ connection string 'messaging' is null or empty. Expected environment variable: ConnectionStrings__messaging");
                throw new InvalidOperationException(
                    "RabbitMQ connection string 'messaging' not found. Expected ConnectionStrings__messaging environment variable.");
            }

            logger.LogDebug("RabbitMQ connection string found (length: {Length})", endpoint.Length);

            var connectionUri = new Uri(endpoint);
            var vhost = string.IsNullOrEmpty(connectionUri.AbsolutePath) || connectionUri.AbsolutePath == "/"
                ? "default (/)"
                : connectionUri.AbsolutePath;
            var username = connectionUri.UserInfo.Split(':')[0];

            logger.LogInformation("📁 Virtual host: {VirtualHost}", vhost);
            logger.LogInformation("🔗 Connecting to RabbitMQ at {Host}:{Port}...", connectionUri.Host,
                connectionUri.Port);

            try
            {
                var factory = new ConnectionFactory
                {
                    Uri = new Uri(endpoint)
                };
                await using var connection = await factory.CreateConnectionAsync();
                logger.LogInformation("✅ RabbitMQ connection test successful (vhost: {VirtualHost})", vhost);
            }
            catch (BrokerUnreachableException ex)
            {
                logger.LogError(ex,
                    "Cannot reach RabbitMQ broker. Connection: {Scheme}://{Username}@{Host}:{Port}{AbsolutePath}",
                    connectionUri.Scheme, username, connectionUri.Host, connectionUri.Port,
                    connectionUri.AbsolutePath);
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Failed to connect to RabbitMQ. Connection: {Scheme}://{Username}@{Host}:{Port}{AbsolutePath}",
                    connectionUri.Scheme, username, connectionUri.Host, connectionUri.Port,
                    connectionUri.AbsolutePath);
                throw;
            }
        });
    }

    private static ILogger CreateLogger(IHostApplicationBuilder builder)
    {
        // Create logger from the service provider if available, otherwise create a temporary one
        var loggerFactory = builder.Services.BuildServiceProvider().GetService<ILoggerFactory>()
                            ?? LoggerFactory.Create(b => b.AddConsole());
        return loggerFactory.CreateLogger("Overflow.Wolverine");
    }
}