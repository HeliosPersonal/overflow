using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Overflow.Common.Health;

/// <summary>
/// Health check that verifies RabbitMQ connectivity.
/// </summary>
public class RabbitMqHealthCheck(
    IConfiguration configuration,
    ILogger<RabbitMqHealthCheck> logger,
    string connectionStringName = "messaging") : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var connectionString = configuration.GetConnectionString(connectionStringName);

            if (string.IsNullOrEmpty(connectionString))
            {
                return HealthCheckResult.Unhealthy(
                    $"RabbitMQ connection string '{connectionStringName}' not configured");
            }

            var connectionUri = new Uri(connectionString);
            var vhost = connectionUri.AbsolutePath.TrimStart('/');
            if (string.IsNullOrEmpty(vhost)) vhost = "/";

            var factory = new ConnectionFactory { Uri = connectionUri };

            await using var connection = await factory.CreateConnectionAsync(cancellationToken);
            await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

            return HealthCheckResult.Healthy(
                $"Connected to {connectionUri.Host}:{connectionUri.Port} (vhost: {vhost})",
                new Dictionary<string, object>
                {
                    ["host"] = connectionUri.Host,
                    ["port"] = connectionUri.Port,
                    ["vhost"] = vhost,
                    ["status"] = "connected"
                });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "RabbitMQ health check failed: {Message}", ex.Message);
            return HealthCheckResult.Unhealthy($"Cannot connect to RabbitMQ: {ex.Message}", ex);
        }
    }
}