using Common.Extensions;
using Infrastructure;
using Microsoft.Extensions.Hosting;

namespace Cluster.Monitoring.Online;

public static class OnlineExtensions
{
    // The coordinator only: one tracker per deploy. Every host reads the result through
    // IDeploymentState<OnlineStatsLiveData> (registered by AddMonitoring).
    public static IHostApplicationBuilder AddOnlineTracker(this IHostApplicationBuilder builder)
    {
        builder.Add<OnlineDaysStore>()
               .As<IOnlineDaysStore>();

        builder.Add<OnlineTracker>()
               .As<ICoordinatorSetupCompleted>();

        return builder;
    }
}
