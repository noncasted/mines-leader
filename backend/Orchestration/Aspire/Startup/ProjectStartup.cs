using Infrastructure;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Aspire;

public static class ProjectStartup
{
    public static async Task SetupDb(this IDistributedApplicationBuilder builder, string dbConnection)
    {
        Console.WriteLine("[Startup] Startup in progress");

        await using var connection = await GetConnection();

        var isStorageExists = await IsTableExists("orleansstorage");

        if (isStorageExists == false)
        {
            var sqlFiles = new[]
            {
                "Startup/PostgreSQL-Main.sql",
                "Startup/PostgreSQL-Persistence.sql",
                "Startup/PostgreSQL-Clustering.sql",
                "Startup/PostgreSQL-Clustering-3.7.0.sql"
            };

            foreach (var file in sqlFiles)
            {
                var script = await File.ReadAllTextAsync(file);

                await using var command = new NpgsqlCommand(script, connection);
                await command.ExecuteNonQueryAsync();
            }
        }
        else
        {
            const string membershipTruncateQuery = "TRUNCATE TABLE orleansmembershiptable;";
            await using var membershipTruncateCommand = new NpgsqlCommand(membershipTruncateQuery, connection);
            await membershipTruncateCommand.ExecuteNonQueryAsync();
        }

        foreach (var tableName in States.StateTables)
            await CreateGrainStorageTable(tableName);

        var requiresDrop = builder.Configuration.GetSection("Local").GetSection("ClearStates").Get<bool>();

        if (requiresDrop == true)
        {
  
        }
        
        var requiresCleanup = builder.Configuration.GetSection("Local").GetSection("ClearStates").Get<bool>();

        if (requiresCleanup == true)
        {
            Console.WriteLine("[Startup] Cleaning up grain states");

            foreach (var tableName in States.StateTables)
            {
                var cleanupQuery = $"TRUNCATE TABLE {tableName};";
                await using var cleanupCommand = new NpgsqlCommand(cleanupQuery, connection);
                await cleanupCommand.ExecuteNonQueryAsync();
            }
        }
        
        Console.WriteLine("[Startup] Startup completed");

        return;

        async Task<NpgsqlConnection> GetConnection()
        {
            var safeGuard = 0;

            while (safeGuard < 10)
            {
                safeGuard++;

                try
                {
                    var newConnection = new NpgsqlConnection(dbConnection);
                    await newConnection.OpenAsync();
                    return newConnection;
                }
                catch (Exception e)
                {
                    Console.WriteLine("Failed to connect to database: {0}", e.Message);
                }

                await Task.Delay(TimeSpan.FromSeconds(1));
            }

            throw new Exception();
        }

        async Task CreateGrainStorageTable(string tableName)
        {
            if (await IsTableExists(tableName.ToLower()) == true)
                return;

            var script = await File.ReadAllTextAsync("Startup/NamedGrainStorage.sql");

            script = script.Replace("TABLE_NAME", tableName);

            await using var command = new NpgsqlCommand(script, connection);
            await command.ExecuteNonQueryAsync();
        }

        async Task<bool> IsTableExists(string tableName)
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
    }
}