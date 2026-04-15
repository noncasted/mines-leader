using Common.Extensions;
using Infrastructure;
using Microsoft.Extensions.Hosting;

namespace Cluster.Monitoring;

public static class MonitoringExtensions
{
    public static IHostApplicationBuilder AddMonitoring(this IHostApplicationBuilder builder)
    {
        builder.AddDynamicState<MatchmakingLiveData>();
        builder.AddDynamicState<LiveMatchesData>();
        builder.AddDynamicState<ConnectedUsersLiveData>();
        builder.AddDynamicState<SideEffectsLiveData>();

        return builder;
    }
}