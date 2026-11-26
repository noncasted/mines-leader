using System.Diagnostics;
using Cluster.Discovery;
using Cluster.Monitoring;
using Cluster.Monolith;
using Common.Extensions;
using FluentAssertions;
using Infrastructure.Telemetry;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Xunit;

namespace Framework.Tests.Cluster.Monolith;

// A separate service owns its tracer, meter and logger providers; the hosts of a monolith own only their logger
// providers, and the one tracer provider of the process exports a span of a static source once, named by the process.
public class MonolithTelemetryTests
{
    private static readonly ActivitySource Source = new("Framework.Tests.MonolithTelemetry");

    [Fact]
    public void SeparateService_OwnsAllThreeProviders()
    {
        using var app = Host(new() { ["SERVICE_NAME"] = "meta" }, "Meta");

        app.Services.GetService<TracerProvider>().Should().NotBeNull();
        app.Services.GetService<MeterProvider>().Should().NotBeNull();
        app.Services.GetService<LoggerProvider>().Should().NotBeNull();
    }

    [Fact]
    public void MetricsExportOff_NoMeterProvider()
    {
        using var app = Host(new() { [TelemetrySettings.ExportMetricsKey] = "false" }, "Meta");

        app.Services.GetService<MeterProvider>().Should().BeNull();
        app.Services.GetService<TracerProvider>().Should().NotBeNull();
    }

    [Fact]
    public async Task MonolithHosts_OwnOnlyLogs_ProcessExportsEachSpanOnce()
    {
        var hostSettings = new Dictionary<string, string?> { [TelemetrySettings.ProcessProvidersKey] = "true" };
        using var meta = Host(hostSettings, "Meta");
        using var game = Host(hostSettings, "Game");
        var process = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["SERVICE_NAME"] = "test-deploy"
        }).Build();

        meta.Services.GetService<TracerProvider>().Should().BeNull();
        meta.Services.GetService<MeterProvider>().Should().BeNull();
        meta.Services.GetService<LoggerProvider>().Should().NotBeNull();

        await using var telemetry = MonolithTelemetry.Create(process, [meta.Services, game.Services]);
        var started = new List<string>();
        telemetry.Traces.AddProcessor(new CollectingProcessor(started));

        Source.StartActivity("Probe")!.Dispose();
        new ActivitySource("Meta").StartActivity("OfTheHost")!.Dispose();

        lock (started)
            started.Should().Equal(Source.Name, "Meta");

        var attributes = telemetry.Traces.GetResource().Attributes.ToDictionary(pair => pair.Key, pair => pair.Value);
        attributes["service.name"].Should().Be("test-deploy");
        telemetry.Metrics.Should().NotBeNull();
    }

    [Fact]
    public async Task MonolithInDevelopment_WritesOneSnapshotNamedByTheProcess()
    {
        var directory = Directory.CreateTempSubdirectory("monolith-snapshot-").FullName;
        var hostSettings = DevelopmentHostSettings();
        using var meta = Host(hostSettings, "Meta", Environments.Development, snapshots: true);
        using var game = Host(hostSettings, "Game", Environments.Development, snapshots: true);
        var counter = BackendMeter.Instance.CreateCounter<long>("framework.tests.monolith.snapshot");

        try
        {
            await using (MonolithTelemetry.Create(Process("test-deploy"), [meta.Services, game.Services],
                             () => directory))
            {
                counter.Add(3);
            }

            // Written when the telemetry of the process is disposed, the last snapshot.
            Directory.GetFiles(directory).Select(Path.GetFileName).Should().Equal("metrics_test-deploy.json");
            var json = await File.ReadAllTextAsync(Path.Combine(directory, "metrics_test-deploy.json"));
            json.Should().Contain("framework.tests.monolith.snapshot").And.Contain("\"service\": \"test-deploy\"");
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task NoHostWithSnapshots_NoProcessSnapshot()
    {
        var hostSettings = DevelopmentHostSettings();
        using var meta = Host(hostSettings, "Meta", Environments.Development, snapshots: false);
        using var game = Host(hostSettings, "Game", Environments.Production, snapshots: true);

        await using var telemetry = MonolithTelemetry.Create(Process("test-deploy"), [meta.Services, game.Services],
            () => throw new InvalidOperationException("the directory is resolved only for a snapshot"));

        telemetry.Traces.Should().NotBeNull();
    }

    // No dev file log: it would write into backend/.telemetry of the repository.
    private static Dictionary<string, string?> DevelopmentHostSettings()
    {
        return new Dictionary<string, string?>
        {
            [TelemetrySettings.ProcessProvidersKey] = "true",
            ["FileLogging:Enabled"] = "false"
        };
    }

    private static IConfiguration Process(string serviceName)
    {
        return new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["SERVICE_NAME"] = serviceName
        }).Build();
    }

    private static WebApplication Host(
        Dictionary<string, string?> settings,
        string applicationName,
        string environment = "Production",
        bool snapshots = false)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = applicationName,
            EnvironmentName = environment
        });
        builder.Configuration.AddInMemoryCollection(settings);
        builder.AddTelemetryDefaults();

        if (snapshots)
        {
            builder.Services.AddSingleton<IServiceEnvironment>(new ServiceEnvironment
            {
                Tag = new ServiceTag(applicationName)
            });
            builder.AddMetricsSnapshots();
        }

        builder.AddTraceSource(Source);
        builder.AddBackendMeter();
        return builder.Build();
    }

    private class CollectingProcessor(List<string> started) : BaseProcessor<Activity>
    {
        public override void OnStart(Activity activity)
        {
            lock (started)
                started.Add(activity.Source.Name);
        }
    }
}
