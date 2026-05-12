using Common;
using Common.Extensions;
using Microsoft.Extensions.Configuration;

namespace Aspire.Startup;

public static class StatesCleanup
{
    public static async Task Run(IConfigurationManager configuration)
    {
        await using var connection = await configuration.GetConnection();

        foreach (var info in StatesLookup.All)
        {
            await connection.Truncate(info.TableName);
        }

        await connection.Truncate(DbLookup.SE_Queue);
        await connection.Truncate(DbLookup.SE_Processing);
        await connection.Truncate(DbLookup.SE_Retry);

        // Marten event sourcing tables
        await connection.Truncate("mt_streams");
        await connection.Truncate("mt_events");
        await connection.Truncate("mt_event_progression");
    }
}
