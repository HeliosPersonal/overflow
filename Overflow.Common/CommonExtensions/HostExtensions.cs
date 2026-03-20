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
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(HostExtensions));

        var contextName = typeof(TContext).Name;
        logger.LogInformation("Starting migration for {ContextName}", contextName);

        try
        {
            var context = scope.ServiceProvider.GetRequiredService<TContext>();
            await context.Database.MigrateAsync();
            logger.LogInformation("Migration complete for {ContextName}", contextName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Migration failed for {ContextName}", contextName);
        }
    }
}