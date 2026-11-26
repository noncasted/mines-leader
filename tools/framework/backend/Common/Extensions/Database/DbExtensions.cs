using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace Common.Extensions;

public static class DbExtensions
{
    public static Task<NpgsqlConnection> GetConnection(this IHostApplicationBuilder builder)
    {
        return builder.Configuration.GetConnection();
    }

    // Attempts to open the connection and the pause between them: "PostgresConnect:Attempts" and
    // "PostgresConnect:RetryDelayMs" of the configuration, 10 attempts a second apart by default. Only a transient
    // failure (a database still starting) is retried; any other one, or the last attempt, throws its own exception.
    public static async Task<NpgsqlConnection> GetConnection(this IConfigurationManager configuration)
    {
        var dbConnection = configuration.GetSection("postgres").Get<string>();
        var connect = configuration.GetSection("PostgresConnect");
        var attempts = connect.GetValue("Attempts", 10);
        var retryDelay = TimeSpan.FromMilliseconds(connect.GetValue("RetryDelayMs", 1000));

        if (attempts < 1)
            throw new ArgumentOutOfRangeException(nameof(attempts), attempts, "PostgresConnect:Attempts must be at least 1");

        for (var attempt = 1;; attempt++)
        {
            var connection = new NpgsqlConnection(dbConnection);

            try
            {
                await connection.OpenAsync();
                return connection;
            }
            catch (NpgsqlException e) when (e.IsTransient == true && attempt < attempts)
            {
                // The deploy setup runs without a logger: its console is the log.
                Console.Error.WriteLine($"Failed to connect to database (attempt {attempt} of {attempts}): {e.Message}");
            }
            finally
            {
                if (connection.State != System.Data.ConnectionState.Open)
                    await connection.DisposeAsync();
            }

            await Task.Delay(retryDelay);
        }
    }

    public static async Task CreateIfNotExists(this NpgsqlConnection connection, string tableName, string createSql)
    {
        if (await connection.IsTableExists(tableName) == true)
            return;

        await using var createCommand = new NpgsqlCommand(createSql, connection);
        await createCommand.ExecuteNonQueryAsync();
    }

    public static async Task<bool> IsTableExists(this NpgsqlConnection connection, string tableName)
    {
        var checkTableQuery = $@"
                SELECT EXISTS (
                    SELECT 1
                    FROM information_schema.tables
                    WHERE table_schema = 'public' AND table_name = '{tableName}'
                );";

        await using var checkTableCommand = new NpgsqlCommand(checkTableQuery, connection);
        var result = await checkTableCommand.ExecuteScalarAsync();

        if (result is not bool tableExists)
            throw new Exception("Failed to check if table exists");

        return tableExists;
    }

    public static async Task Truncate(this NpgsqlConnection connection, string tableName)
    {
        if (await connection.IsTableExists(tableName) == false)
            return;

        var truncateQuery = $"TRUNCATE TABLE {tableName} CASCADE;";
        await using var truncateCommand = new NpgsqlCommand(truncateQuery, connection);
        await truncateCommand.ExecuteNonQueryAsync();
    }

    public static async Task Drop(this NpgsqlConnection connection, string tableName)
    {
        if (await connection.IsTableExists(tableName) == false)
            return;

        var truncateQuery = $"DROP TABLE IF EXISTS {tableName} CASCADE;";
        await using var truncateCommand = new NpgsqlCommand(truncateQuery, connection);
        await truncateCommand.ExecuteNonQueryAsync();
    }
}