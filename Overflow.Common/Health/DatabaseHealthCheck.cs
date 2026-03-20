using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Overflow.Common.Health;

/// <summary>
/// Health check that verifies PostgreSQL connectivity for any DbContext.
/// </summary>
public class DatabaseHealthCheck<TDbContext>(
    TDbContext context,
    ILogger<DatabaseHealthCheck<TDbContext>> logger) : IHealthCheck
    where TDbContext : DbContext
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext healthContext,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var canConnect = await context.Database.CanConnectAsync(cancellationToken);

            if (!canConnect)
            {
                return HealthCheckResult.Unhealthy("Cannot connect to database",
                    data: new Dictionary<string, object> { ["status"] = "connection_failed" });
            }

            var dbName = ExtractDatabaseName(context.Database.GetConnectionString());

            return HealthCheckResult.Healthy($"Connected to '{dbName}'",
                new Dictionary<string, object>
                {
                    ["database"] = dbName,
                    ["status"] = "connected"
                });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Database health check failed: {Message}", ex.Message);
            return HealthCheckResult.Unhealthy($"Cannot connect to database: {ex.Message}", ex);
        }
    }

    private static string ExtractDatabaseName(string? connectionString)
    {
        if (string.IsNullOrEmpty(connectionString))
            return "unknown";

        try
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString);
            return !string.IsNullOrEmpty(builder.Database) ? builder.Database : "unknown";
        }
        catch
        {
            return "unknown";
        }
    }
}