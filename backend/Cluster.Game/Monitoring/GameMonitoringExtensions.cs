using Cluster.Deploy;
using Microsoft.Extensions.Hosting;

namespace Cluster.Monitoring;

public static class GameMonitoringExtensions
{
    // Live data of the game for the console. The cluster live data comes from the framework (AddMonitoring).
    public static IHostApplicationBuilder AddGameMonitoring(this IHostApplicationBuilder builder)
    {
        builder.AddDeploymentState<MatchmakingLiveData>();
        builder.AddDeploymentState<LiveMatchesData>();

        return builder;
    }
}
