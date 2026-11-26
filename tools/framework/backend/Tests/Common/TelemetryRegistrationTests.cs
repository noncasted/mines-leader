using System.Diagnostics;
using Common.Extensions;
using FluentAssertions;
using Infrastructure;
using Infrastructure.Execution;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Xunit;

namespace Framework.Tests.Common;

// The features register their sources and the meter themselves: a service that adds OpenTelemetry without listing
// them still gets their traces and metrics.
public class TelemetryRegistrationTests
{
    public static TheoryData<string> Features => ["OrleansUtils", "Messaging", "TaskScheduling"];

    [Theory]
    [MemberData(nameof(Features))]
    public void Feature_RegistersItsTraceSources(string feature)
    {
        var started = new List<string>();
        var builder = Host.CreateApplicationBuilder();
        var sources = Register(builder, feature);
        builder.Services.AddOpenTelemetry()
               .WithTracing(tracing => tracing.AddProcessor(new CollectingProcessor(started)));

        using var host = builder.Build();
        host.Services.GetRequiredService<TracerProvider>();

        foreach (var source in sources)
            source.StartActivity("Probe")!.Dispose();

        lock (started)
            started.Should().Contain(sources.Select(source => source.Name));
    }

    [Theory]
    [MemberData(nameof(Features))]
    public void Feature_RegistersTheBackendMeter(string feature)
    {
        var exporter = new CollectingExporter();
        var builder = Host.CreateApplicationBuilder();
        Register(builder, feature);
        builder.Services.AddOpenTelemetry()
               .WithMetrics(metrics => metrics.AddReader(new BaseExportingMetricReader(exporter)));

        using var host = builder.Build();
        var provider = host.Services.GetRequiredService<MeterProvider>();
        TransactionTelemetry.Total.Add(0);
        provider.ForceFlush();

        exporter.Meters.Should().Contain(BackendMeter.Name);
    }

    private static IReadOnlyList<ActivitySource> Register(IHostApplicationBuilder builder, string feature)
    {
        switch (feature)
        {
            case "OrleansUtils":
                builder.AddOrleansUtils();
                return [TransactionTelemetry.Source, SideEffectTelemetry.Source];
            case "Messaging":
                builder.AddMessaging();
                return [DurableQueueTelemetry.Source, RuntimePipeTelemetry.Source, RuntimeChannelTelemetry.Source];
            case "TaskScheduling":
                builder.AddTaskScheduling();
                return [TaskBalancerTelemetry.Source];
            default:
                throw new ArgumentOutOfRangeException(nameof(feature), feature, null);
        }
    }

    private class CollectingProcessor : BaseProcessor<Activity>
    {
        public CollectingProcessor(List<string> started)
        {
            _started = started;
        }

        private readonly List<string> _started;

        public override void OnStart(Activity activity)
        {
            lock (_started)
                _started.Add(activity.Source.Name);
        }
    }

    private class CollectingExporter : BaseExporter<Metric>
    {
        private readonly HashSet<string> _meters = new();

        public IReadOnlyCollection<string> Meters => _meters;

        public override ExportResult Export(in Batch<Metric> batch)
        {
            foreach (var metric in batch)
                _meters.Add(metric.MeterName);

            return ExportResult.Success;
        }
    }
}
