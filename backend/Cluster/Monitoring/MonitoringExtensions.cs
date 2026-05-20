using Cluster.Deploy;
using Microsoft.Extensions.Hosting;

namespace Cluster.Monitoring;

public static class MonitoringExtensions
{
    public static IHostApplicationBuilder AddMonitoring(this IHostApplicationBuilder builder)
    {
        builder.AddDeploymentState<MatchmakingLiveData>();
        builder.AddDeploymentState<LiveMatchesData>();
        builder.AddDeploymentState<ConnectedUsersLiveData>();

        return builder;
    }
}