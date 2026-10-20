using Common;
using Shared;
using Common.Extensions;
using Microsoft.Extensions.Configuration;

namespace DeploySetup;

public class StatesDrop
{
    public static async Task Run(IConfigurationManager configuration)
    {
        await using var connection = await configuration.GetConnection();

        foreach (var info in StatesLookup.All)
            await connection.Drop(info.TableName);

        // The same side effect tables as SideEffectsSetup creates.
        await connection.Drop(DbLookup.SE_Queue);
        await connection.Drop(DbLookup.SE_Processing);
        await connection.Drop(DbLookup.SE_Retry);
        await connection.Drop(DbLookup.SE_DeadLetter);

        // Marten event sourcing tables
        await connection.Drop("mt_streams");
        await connection.Drop("mt_events");
        await connection.Drop("mt_event_progression");

        // Benchmark state tables too (StatesLookup.All): a database migrated with benchmarks on drops them as well.
    }
}