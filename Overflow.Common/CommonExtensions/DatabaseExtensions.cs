using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Overflow.Common.CommonExtensions;

/// <summary>
/// Extension methods for Entity Framework Core database operations.
/// </summary>
public static class DatabaseExtensions
{
    /// <summary>
    /// Applies pending EF Core migrations automatically on application startup.
    /// This is useful for development and staging environments.
    /// </summary>
    /// <typeparam name="TContext">The DbContext type to migrate.</typeparam>
    /// <param name="app">The web application.</param>
    /// <param name="seedData">Optional action to seed data after migration.</param>
    public static async Task MigrateDatabaseAsync<TContext>(this WebApplication app,
        Func<TContext, Task>? seedData = null) 
        where TContext : DbContext
    {
        using var scope = app.Services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<TContext>>();
        
        try
        {
            logger.LogInformation("🗄️ Starting database migration for {ContextName}...", typeof(TContext).Name);
            
            // Try to get the context - if this fails, it might be due to missing connection string
            TContext context;
            try
            {
                context = scope.ServiceProvider.GetRequiredService<TContext>();
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("ConnectionString is missing"))
            {
                logger.LogWarning("⚠️ Database connection string is not configured. Skipping migration.");
                logger.LogInformation("ℹ️ This is expected when running outside of the Aspire AppHost orchestration.");
                logger.LogInformation("ℹ️ To run with database support, either:");
                logger.LogInformation("   - Run the application through the Aspire AppHost (recommended)");
                logger.LogInformation("   - Configure connection string in appsettings.json or environment variables");
                return;
            }
            
            // Check if there are pending migrations
            var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
            var pendingCount = pendingMigrations.Count();
            
            if (pendingCount > 0)
            {
                logger.LogInformation("📊 Found {Count} pending migrations", pendingCount);
                await context.Database.MigrateAsync();
                logger.LogInformation("✅ Database migration completed successfully");
            }
            else
            {
                logger.LogInformation("✅ Database is up to date, no migrations needed");
            }
            
            // Seed data if provided
            if (seedData != null)
            {
                logger.LogInformation("🌱 Seeding database...");
                await seedData(context);
                logger.LogInformation("✅ Database seeding completed");
            }
        }
        catch (Exception ex)
        {
            // Check if this is a connection string configuration issue
            if (ex is InvalidOperationException && ex.Message.Contains("ConnectionString is missing"))
            {
                logger.LogWarning("⚠️ Database connection string is not configured. Skipping migration.");
                logger.LogInformation("ℹ️ This is expected when running outside of the Aspire AppHost orchestration.");
                logger.LogInformation("ℹ️ To run with database support, either:");
                logger.LogInformation("   - Run the application through the Aspire AppHost (recommended)");
                logger.LogInformation("   - Configure connection string in appsettings.json or environment variables");
                return;
            }
            
            logger.LogError(ex, "❌ An error occurred while migrating or seeding the database: {Message}", ex.Message);
            
            // In development, we might want to throw to make the error visible
            // In production, we might want to continue and let health checks catch it
            if (app.Environment.IsDevelopment())
            {
                throw;
            }
            
            logger.LogWarning("⚠️ Application will continue, but database may not be properly initialized");
        }
    }
    
    /// <summary>
    /// Ensures the database is created (for simple scenarios without migrations).
    /// Use MigrateDatabaseAsync for production scenarios with migrations.
    /// </summary>
    /// <typeparam name="TContext">The DbContext type.</typeparam>
    /// <param name="app">The web application.</param>
    /// <returns>The web application for chaining.</returns>
    public static async Task<WebApplication> EnsureDatabaseCreatedAsync<TContext>(
        this WebApplication app) 
        where TContext : DbContext
    {
        using var scope = app.Services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<TContext>>();
        
        try
        {
            logger.LogInformation("🗄️ Ensuring database exists for {ContextName}...", typeof(TContext).Name);
            
            var context = scope.ServiceProvider.GetRequiredService<TContext>();
            var created = await context.Database.EnsureCreatedAsync();
            
            if (created)
            {
                logger.LogInformation("✅ Database created successfully");
            }
            else
            {
                logger.LogInformation("✅ Database already exists");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ An error occurred while ensuring database exists: {Message}", ex.Message);
            throw;
        }
        
        return app;
    }
    
    /// <summary>
    /// Checks database connectivity without performing migrations.
    /// Useful for health checks and startup validation.
    /// </summary>
    /// <typeparam name="TContext">The DbContext type.</typeparam>
    /// <param name="app">The web application.</param>
    /// <returns>True if database is accessible, false otherwise.</returns>
    public static async Task<bool> CheckDatabaseConnectionAsync<TContext>(
        this WebApplication app) 
        where TContext : DbContext
    {
        using var scope = app.Services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<TContext>>();
        
        try
        {
            logger.LogInformation("🔍 Checking database connection for {ContextName}...", typeof(TContext).Name);
            
            var context = scope.ServiceProvider.GetRequiredService<TContext>();
            var canConnect = await context.Database.CanConnectAsync();
            
            if (canConnect)
            {
                logger.LogInformation("✅ Database connection successful");
            }
            else
            {
                logger.LogWarning("⚠️ Cannot connect to database");
            }
            
            return canConnect;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Database connection check failed: {Message}", ex.Message);
            return false;
        }
    }
}

