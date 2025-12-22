using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Overflow.Common.Health;

/// <summary>
/// Generic health check that verifies RabbitMQ connectivity and virtual host accessibility.
/// </summary>
public class RabbitMqHealthCheck : IHealthCheck
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<RabbitMqHealthCheck> _logger;
    private readonly string _connectionStringName;

    public RabbitMqHealthCheck(
        IConfiguration configuration,
        ILogger<RabbitMqHealthCheck> logger)
        : this(configuration, logger, "messaging")
    {
    }

    public RabbitMqHealthCheck(
        IConfiguration configuration,
        ILogger<RabbitMqHealthCheck> logger,
        string connectionStringName)
    {
        _configuration = configuration;
        _logger = logger;
        _connectionStringName = connectionStringName;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var connectionString = _configuration.GetConnectionString(_connectionStringName);

            if (string.IsNullOrEmpty(connectionString))
            {
                return HealthCheckResult.Unhealthy(
                    $"RabbitMQ connection string '{_connectionStringName}' not configured",
                    data: new Dictionary<string, object>
                    {
                        ["status"] = "not_configured",
                        ["connectionStringName"] = _connectionStringName
                    });
            }

            var connectionUri = new Uri(connectionString);
            var vhost = connectionUri.AbsolutePath.TrimStart('/');
            if (string.IsNullOrEmpty(vhost))
            {
                vhost = "/";
            }

            var factory = new ConnectionFactory
            {
                Uri = new Uri(connectionString)
            };

            // Test connection
            await using var connection = await factory.CreateConnectionAsync(cancellationToken);
            await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

            var data = new Dictionary<string, object>
            {
                ["host"] = connectionUri.Host,
                ["port"] = connectionUri.Port,
                ["vhost"] = vhost,
                ["status"] = "connected"
            };

            _logger.LogDebug("RabbitMQ health check passed: {Host}:{Port} vhost={VHost}",
                connectionUri.Host, connectionUri.Port, vhost);

            return HealthCheckResult.Healthy(
                $"RabbitMQ is healthy. Connected to {connectionUri.Host}:{connectionUri.Port} (vhost: {vhost})",
                data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RabbitMQ health check failed: {Message}", ex.Message);

            return HealthCheckResult.Unhealthy(
                $"Cannot connect to RabbitMQ: {ex.Message}",
                ex,
                new Dictionary<string, object>
                {
                    ["status"] = "connection_failed",
                    ["error"] = ex.Message
                });
        }
    }
}

