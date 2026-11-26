using Cluster.Deploy;
using Cluster.Monitoring.Online;
using Microsoft.Extensions.DependencyInjection;
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

    // The dev snapshots of BackendMeter in backend/.telemetry/metrics (MetricsSnapshotService).
    public static IHostApplicationBuilder AddMetricsSnapshots(this IHostApplicationBuilder builder)
    {
        builder.Services.AddSingleton(new MetricsSnapshotRegistration());
        builder.Services.AddHostedService<MetricsSnapshotService>();

        return builder;
    }
}
