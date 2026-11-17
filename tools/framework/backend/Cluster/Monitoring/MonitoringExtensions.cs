using Cluster.Deploy;
using Cluster.Monitoring.Online;
using Microsoft.Extensions.Hosting;

namespace Cluster.Monitoring;

public static class MonitoringExtensions
{
    public static IHostApplicationBuilder AddMonitoring(this IHostApplicationBuilder builder)
    {
        builder.AddServiceSlicedState<LiveSessionsData, List<LiveSessionEntry>>();
        builder.AddServiceSlicedState<ConnectedUsersLiveData, List<ConnectedUserEntry>>();
        builder.AddDeploymentState<OnlineStatsLiveData>();

        return builder;
    }
}
