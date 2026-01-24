using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Overflow.Common.CommonExtensions;

public static class HostExtensions
{
    public static async Task MigrateDbContextAsync<TContext>(this IHost host)
        where TContext : DbContext
    {
        using var scope = host.Services.CreateScope();
        var services = scope.ServiceProvider;
        var loggerFactory = services.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger(typeof(HostExtensions));

        logger.LogInformation("Migration started for {Name}", typeof(TContext).Name);

        try
        {
            var context = services.GetRequiredService<TContext>();
            await context.Database.MigrateAsync();
        }
        catch (Exception e)
        {
            logger.LogError(e, "An error occurred while migrating or seeding the database.");
        }

        logger.LogInformation("✅ Migration complete for {Name}", typeof(TContext).Name);
    }
}