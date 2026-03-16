using Common;
using Common.Extensions;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Aspire;

public static class OrleansSetup
{
    public static async Task Run(IConfigurationManager configuration)
    {
        await using var connection = await configuration.GetConnection();

        if (await connection.IsTableExists(DbLookup.Orleans_Storage) == true)
            return;

        var sqlFiles = new[]
        {
            "Startup/Queries/PostgreSQL-Main.sql",
            "Startup/Queries/PostgreSQL-Persistence.sql",
            "Startup/Queries/PostgreSQL-Clustering.sql",
            "Startup/Queries/PostgreSQL-Clustering-3.7.0.sql"
        };

        foreach (var file in sqlFiles)
        {
            var script = await File.ReadAllTextAsync(file);
            await using var command = new NpgsqlCommand(script, connection);
            await command.ExecuteNonQueryAsync();
        }
    }
}