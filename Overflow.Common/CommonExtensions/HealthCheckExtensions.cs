using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Overflow.Common.CommonExtensions;

public static class HealthCheckExtensions
{
    /// <summary>
    /// Adds RabbitMQ health check that verifies connectivity and virtual host accessibility.
    /// Uses the generic RabbitMqHealthCheck implementation from Overflow.Common.Health.
    /// Tagged with "ready" for Kubernetes readiness probe.
    /// </summary>
    /// <param name="builder">The health checks builder.</param>
    /// <param name="connectionStringName">The connection string name. Defaults to "messaging".</param>
    /// <returns>The builder for chaining.</returns>
    public static IHealthChecksBuilder AddRabbitMqHealthCheck(
        this IHealthChecksBuilder builder,
        string connectionStringName = "messaging")
    {
        builder.Services.AddSingleton(sp => 
            new Health.RabbitMqHealthCheck(
                sp.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Health.RabbitMqHealthCheck>>(),
                connectionStringName));

        builder.AddCheck<Health.RabbitMqHealthCheck>(
            name: "rabbitmq",
            failureStatus: HealthStatus.Unhealthy,
            tags: ["ready", "messaging", "rabbitmq"]);

        return builder;
    }

    /// <summary>
    /// Adds Typesense health check that verifies connectivity and server health.
    /// Uses the generic TypesenseHealthCheck implementation from Overflow.Common.Health.
    /// Tagged with "ready" for Kubernetes readiness probe.
    /// </summary>
    /// <param name="builder">The health checks builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static IHealthChecksBuilder AddTypesenseHealthCheck(
        this IHealthChecksBuilder builder)
    {
        builder.AddCheck<Health.TypesenseHealthCheck>(
            name: "typesense",
            failureStatus: HealthStatus.Unhealthy,
            tags: ["ready", "search", "typesense"]);

        return builder;
    }

    /// <summary>
    /// Adds Database (PostgreSQL/SQL Server) health check that verifies connectivity.
    /// Uses the generic DatabaseHealthCheck implementation from Overflow.Common.Health.
    /// Tagged with "ready" for Kubernetes readiness probe.
    /// </summary>
    /// <typeparam name="TDbContext">The DbContext type to check connectivity for.</typeparam>
    /// <param name="builder">The health checks builder.</param>
    /// <param name="name">The name for this health check. Defaults to "database".</param>
    /// <returns>The builder for chaining.</returns>
    public static IHealthChecksBuilder AddDatabaseHealthCheck<TDbContext>(
        this IHealthChecksBuilder builder,
        string name = "database")
        where TDbContext : Microsoft.EntityFrameworkCore.DbContext
    {
        builder.AddCheck<Health.DatabaseHealthCheck<TDbContext>>(
            name: name,
            failureStatus: HealthStatus.Unhealthy,
            tags: ["ready", "db", "database"]);

        return builder;
    }
}

