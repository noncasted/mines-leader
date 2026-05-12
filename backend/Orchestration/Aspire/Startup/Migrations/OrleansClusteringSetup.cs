using Common.Extensions;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Aspire.Startup;

// Bootstraps the schema required by Orleans AdoNet clustering provider (OrleansQuery,
// OrleansMembershipVersionTable, OrleansMembershipTable + helper functions). Upstream
// scripts are not idempotent, so we gate on the OrleansQuery table already existing.
public static class OrleansClusteringSetup
{
    private const string MarkerTable = "orleansquery";

    // Full bootstrap only runs once (gated on MarkerTable). Supplemental runs every
    // time because it is already idempotent and patches missing rows on existing DBs.
    private static readonly string[] BootstrapResources =
    [
        "PostgreSQL-Main.sql",
        "PostgreSQL-Clustering.sql"
    ];

    private const string SupplementalResource = "PostgreSQL-Supplemental.sql";

    public static async Task Run(IConfigurationManager configuration)
    {
        await using var connection = await configuration.GetConnection();

        if (!await connection.IsTableExists(MarkerTable))
        {
            foreach (var resourceName in BootstrapResources)
                await ExecuteSqlFile(connection, resourceName);
        }

        await ExecuteSqlFile(connection, SupplementalResource);
    }

    private static async Task ExecuteSqlFile(NpgsqlConnection connection, string fileName)
    {
        var sql = await ReadSqlFile(fileName);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<string> ReadSqlFile(string fileName)
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "Startup", "Migrations", "Sql");
        var path = Path.Combine(dir, fileName);
        return await File.ReadAllTextAsync(path);
    }
}
