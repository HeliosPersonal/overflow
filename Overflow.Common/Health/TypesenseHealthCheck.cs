using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Typesense;

namespace Overflow.Common.Health;

/// <summary>
/// Generic health check that verifies Typesense connectivity and server health.
/// </summary>
public class TypesenseHealthCheck : IHealthCheck
{
    private readonly ITypesenseClient _client;
    private readonly ILogger<TypesenseHealthCheck> _logger;

    public TypesenseHealthCheck(
        ITypesenseClient client,
        ILogger<TypesenseHealthCheck> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Check Typesense server by listing collections
            var collections = await _client.RetrieveCollections(cancellationToken);

            var data = new Dictionary<string, object>
            {
                ["status"] = "healthy",
                ["collectionsCount"] = collections.Count
            };

            _logger.LogDebug("Typesense health check passed. Found {Count} collections", collections.Count);

            return HealthCheckResult.Healthy(
                $"Typesense is healthy. Found {collections.Count} collection(s)",
                data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Typesense health check failed: {Message}", ex.Message);

            return HealthCheckResult.Unhealthy(
                $"Cannot connect to Typesense: {ex.Message}",
                ex,
                new Dictionary<string, object>
                {
                    ["status"] = "connection_failed",
                    ["error"] = ex.Message
                });
        }
    }
}

