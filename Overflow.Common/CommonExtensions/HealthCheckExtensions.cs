using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Overflow.Common.CommonExtensions;

public static class HealthCheckExtensions
{
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

    public static IHealthChecksBuilder AddTypesenseHealthCheck(
        this IHealthChecksBuilder builder)
    {
        builder.AddCheck<Health.TypesenseHealthCheck>(
            name: "typesense",
            failureStatus: HealthStatus.Unhealthy,
            tags: ["ready", "search", "typesense"]);

        return builder;
    }

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