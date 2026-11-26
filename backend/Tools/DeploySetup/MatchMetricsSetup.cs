using Common.Extensions;
using Game.MatchMetrics;
using Microsoft.Extensions.Configuration;

namespace DeploySetup;

// The tables of the game data of matches (framework MatchMetricsSchema), each created only when missing.
public static class MatchMetricsSetup
{
    public static async Task Run(IConfigurationManager configuration)
    {
        await using var connection = await configuration.GetConnection();
        await MatchMetricsSchema.Create(connection);
    }
}
