using Cluster.Monitoring;
using Common.Extensions;
using Infrastructure;
using Infrastructure.Telemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Cluster.Monolith;

// The telemetry of a monolith process: one tracer provider and one meter provider for all its hosts, named by the
// configuration of the process (service.name = the deploy). The sources of the framework and of ASP.NET Core, the
// HttpClient and Orleans are static and heard by name in the whole process: a provider per host would export every
// span once per host, and a span of a static source cannot tell its host. The hosts keep their logs
// (Telemetry:ProcessProviders); the runner logs through the logger of the process, named like its spans. The dev
// metrics snapshot is one per process for the same reason (BackendMeter is static): metrics_<deploy>.json, written
// when a host in Development has AddMetricsSnapshots; the MetricsSnapshotService of the hosts does nothing.
public sealed class MonolithTelemetry : IAsyncDisposable
{
    private MonolithTelemetry(ServiceProvider services, MetricsSnapshotWriter? snapshot)
    {
        _services = services;
        _snapshot = snapshot;
        Traces = services.GetRequiredService<TracerProvider>();
        Metrics = services.GetService<MeterProvider>();
        Logs = services.GetRequiredService<ILoggerFactory>();
    }

    private readonly ServiceProvider _services;
    private readonly MetricsSnapshotWriter? _snapshot;

    public TracerProvider Traces { get; }

    // Null when Telemetry:ExportMetrics is false.
    public MeterProvider? Metrics { get; }

    public ILoggerFactory Logs { get; }

    // The hosts are built, not started: the sources and meters they registered (AddTraceSource, AddBackendMeter, the
    // source of each host) are added to the providers of the process.
    public static MonolithTelemetry Create(IConfiguration configuration, IReadOnlyList<IServiceProvider> hosts)
    {
        return Create(configuration, hosts, () => TelemetryPaths.RequireTelemetryDir("metrics"));
    }

    public static MonolithTelemetry Create(
        IConfiguration configuration,
        IReadOnlyList<IServiceProvider> hosts,
        Func<string> resolveSnapshotDirectory)
    {
        var settings = TelemetrySettings.Read(configuration, "monolith");
        var services = new ServiceCollection();

        // Before AddOpenTelemetry: the SDK reads OTEL_* from this configuration instead of only the variables.
        services.AddSingleton(configuration);

        services.AddLogging(logging => {
            logging.AddConfiguration(configuration.GetSection("Logging"));
            logging.AddConsole();
            logging.AddTelemetryLogs();
        });

        var sources = hosts.SelectMany(host => host.GetServices<TraceSourceRegistration>())
                           .Select(registration => registration.Name)
                           .Distinct()
                           .ToArray();

        var telemetry = services.AddOpenTelemetry()
                                .WithTelemetryLogs(settings, configuration)
                                .WithTelemetryTracing(settings, configuration, tracing => tracing.AddSource(sources));

        if (settings.ExportsMetrics)
        {
            var meters = hosts.SelectMany(host => host.GetServices<MeterRegistration>())
                              .Select(registration => registration.Name)
                              .Distinct()
                              .ToArray();

            telemetry.WithTelemetryMetrics(settings, configuration, metrics => metrics.AddMeter(meters));
        }

        var provider = services.BuildServiceProvider();
        var snapshot = WritesSnapshot(hosts)
            ? new MetricsSnapshotWriter(provider.GetRequiredService<ILogger<MonolithTelemetry>>(),
                resolveSnapshotDirectory(), settings.LogName)
            : null;

        return new MonolithTelemetry(provider, snapshot);
    }

    private static bool WritesSnapshot(IReadOnlyList<IServiceProvider> hosts)
    {
        return hosts.Any(host => host.GetService<MetricsSnapshotRegistration>() != null &&
                                 host.GetRequiredService<IHostEnvironment>().IsDevelopment());
    }

    // Writes the last snapshot and flushes the exporters of the process.
    public ValueTask DisposeAsync()
    {
        _snapshot?.Dispose();
        return _services.DisposeAsync();
    }
}
