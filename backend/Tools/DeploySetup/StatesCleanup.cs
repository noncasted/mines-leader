using Common;
using Common.Extensions;
using Microsoft.Extensions.Configuration;
using Shared;

namespace DeploySetup;

public static class StatesCleanup
{
    public static async Task Run(IConfigurationManager configuration)
    {
        await using var connection = await configuration.GetConnection();

        foreach (var info in StatesLookup.All)
            await connection.Truncate(info.TableName);

        await connection.Truncate(SideEffectTables.Queue);
        await connection.Truncate(SideEffectTables.Processing);
        await connection.Truncate(SideEffectTables.RetryQueue);
        await connection.Truncate(SideEffectTables.DeadLetter);

        // Marten event sourcing tables
        await connection.Truncate("mt_streams");
        await connection.Truncate("mt_events");
        await connection.Truncate("mt_event_progression");
    }
}