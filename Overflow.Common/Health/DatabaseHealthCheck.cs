using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Overflow.Common.Health;

/// <summary>
/// Generic health check that verifies database connectivity for any DbContext.
/// Supports PostgreSQL, SQL Server, and other EF Core providers.
/// </summary>
/// <typeparam name="TDbContext">The DbContext type to check connectivity for.</typeparam>
public class DatabaseHealthCheck<TDbContext> : IHealthCheck
    where TDbContext : DbContext
{
    private readonly TDbContext _context;
    private readonly ILogger<DatabaseHealthCheck<TDbContext>> _logger;

    public DatabaseHealthCheck(TDbContext context, ILogger<DatabaseHealthCheck<TDbContext>> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Try to connect and execute a simple query
            var canConnect = await _context.Database.CanConnectAsync(cancellationToken);

            if (!canConnect)
            {
                return HealthCheckResult.Unhealthy(
                    "Cannot connect to database",
                    data: new Dictionary<string, object>
                    {
                        ["status"] = "connection_failed"
                    });
            }

            // Get connection info
            var connectionString = _context.Database.GetConnectionString();
            var dbName = ExtractDatabaseName(connectionString);
            var dbProvider = _context.Database.ProviderName ?? "Unknown";

            var data = new Dictionary<string, object>
            {
                ["database"] = dbName,
                ["provider"] = dbProvider,
                ["status"] = "connected"
            };

            _logger.LogDebug("Database health check passed for: {Database} (Provider: {Provider})",
                dbName, dbProvider);

            return HealthCheckResult.Healthy(
                $"Database is healthy. Connected to '{dbName}' ({dbProvider})",
                data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database health check failed: {Message}", ex.Message);

            return HealthCheckResult.Unhealthy(
                $"Cannot connect to database: {ex.Message}",
                ex,
                new Dictionary<string, object>
                {
                    ["status"] = "connection_failed",
                    ["error"] = ex.Message
                });
        }
    }

    private static string ExtractDatabaseName(string? connectionString)
    {
        if (string.IsNullOrEmpty(connectionString))
        {
            return "unknown";
        }

        try
        {
            // Try PostgreSQL connection string
            var npgsqlBuilder = new NpgsqlConnectionStringBuilder(connectionString);
            if (!string.IsNullOrEmpty(npgsqlBuilder.Database))
            {
                return npgsqlBuilder.Database;
            }
        }
        catch
        {
            // Not a PostgreSQL connection string, try other formats
        }

        try
        {
            // Try generic connection string parsing for SQL Server and others
            // Look for Database= or Initial Catalog=
            var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var keyValue = part.Split('=', 2, StringSplitOptions.RemoveEmptyEntries);
                if (keyValue.Length == 2)
                {
                    var key = keyValue[0].Trim();
                    if (key.Equals("Database", StringComparison.OrdinalIgnoreCase) ||
                        key.Equals("Initial Catalog", StringComparison.OrdinalIgnoreCase))
                    {
                        return keyValue[1].Trim();
                    }
                }
            }
        }
        catch
        {
            // Ignore parsing errors
        }

        return "unknown";
    }
}

