using Cluster.Discovery;
using Infrastructure;
using Infrastructure.Telemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Cluster.Monitoring;

// Dev-only (AddMetricsSnapshots): snapshots go to backend/.telemetry/metrics of the repository the service runs from,
// metrics_<role>.json by IServiceEnvironment.Tag. In other environments metrics go through OpenTelemetry only and the
// service does nothing. A host of a monolith (Telemetry:ProcessProviders) does nothing either: BackendMeter is static,
// a writer per host would hold the totals of all roles, so the process writes one snapshot named by the deploy
// (MonolithTelemetry).
internal class MetricsSnapshotService : IHostedService, IDisposable
{
    public MetricsSnapshotService(
        ILogger<MetricsSnapshotService> logger,
        IHostEnvironment environment,
        IConfiguration configuration,
        IServiceEnvironment serviceEnvironment)
        : this(logger, environment, configuration, serviceEnvironment,
            () => TelemetryPaths.RequireTelemetryDir("metrics"))
    {
    }

    public MetricsSnapshotService(
        ILogger<MetricsSnapshotService> logger,
        IHostEnvironment environment,
        IConfiguration configuration,
        IServiceEnvironment serviceEnvironment,
        Func<string> resolveDirectory)
    {
        _logger = logger;
        _environment = environment;
        _usesProcessProviders = TelemetrySettings.Read(configuration, environment.ApplicationName).UsesProcessProviders;
        _serviceName = serviceEnvironment.Tag.Value.ToLowerInvariant();
        _resolveDirectory = resolveDirectory;
    }

    private readonly ILogger<MetricsSnapshotService> _logger;
    private readonly IHostEnvironment _environment;
    private readonly bool _usesProcessProviders;
    private readonly string _serviceName;
    private readonly Func<string> _resolveDirectory;

    private MetricsSnapshotWriter? _writer;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_environment.IsDevelopment() == false)
        {
            _logger.LogInformation("[MetricsSnapshot] File snapshots are disabled outside Development");
            return Task.CompletedTask;
        }

        if (_usesProcessProviders)
        {
            _logger.LogInformation("[MetricsSnapshot] A host of a monolith: the process writes the snapshot");
            return Task.CompletedTask;
        }

        // No repository root in Development is a setup error: fail the start instead of silently writing nothing.
        _writer = new MetricsSnapshotWriter(_logger, _resolveDirectory(), _serviceName);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Dispose();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _writer?.Dispose();
        _writer = null;
    }
}
