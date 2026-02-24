using Npgsql;

namespace Overflow.StatsService.Extensions;

public static class PostgresDbExtensions
{
    public static async Task EnsurePostgresDatabaseExistsAsync(
        this string connectionString, 
        ILogger? logger = null)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        var targetDb = builder.Database;

        builder.Database = "postgres";
        await using var adminConn = new NpgsqlConnection(builder.ToString());
        await adminConn.OpenAsync();

        var existsCommand = new NpgsqlCommand(
            $"SELECT 1 FROM pg_database WHERE datname = @dbName", adminConn);
        existsCommand.Parameters.AddWithValue("dbName", targetDb!);
        var exists = await existsCommand.ExecuteScalarAsync();

        if (exists == null)
        {
            var createCommand = new NpgsqlCommand($"CREATE DATABASE \"{targetDb}\"", adminConn);
            await createCommand.ExecuteNonQueryAsync();
            logger?.LogInformation("Created PostgreSQL database: {Database}", targetDb);
        }
        else
        {
            logger?.LogDebug("PostgreSQL database already exists: {Database}", targetDb);
        }
    }
}