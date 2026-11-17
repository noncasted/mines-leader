using System.Diagnostics;
using System.Diagnostics.Metrics;
using Common.Extensions;

namespace Infrastructure;

public static class SideEffectTelemetry
{
    public static readonly ActivitySource Source = new("Infrastructure.SideEffects");

    public static readonly Counter<long> Processed = BackendMeter.Instance.CreateCounter<long>(
        "backend.side_effects.processed", description: "Side effects processed");

    public static readonly Counter<long> Retry = BackendMeter.Instance.CreateCounter<long>(
        "backend.side_effects.retry", description: "Side effects retried");

    public static readonly Counter<long> Failed = BackendMeter.Instance.CreateCounter<long>(
        "backend.side_effects.failed", description: "Side effects failed");

    public static readonly Counter<long> DeadLetter = BackendMeter.Instance.CreateCounter<long>(
        "backend.side_effects.dead_letter", description: "Side effects moved to dead letter queue");

    public static readonly Histogram<double> Duration = BackendMeter.Instance.CreateHistogram<double>(
        "backend.side_effects.duration", "ms", "Side effect execution duration");

    public static readonly UpDownCounter<int> InProgress = BackendMeter.Instance.CreateUpDownCounter<int>(
        "backend.side_effects.in_progress", description: "Side effects currently executing");

    public static readonly Histogram<int> QueueDepth = BackendMeter.Instance.CreateHistogram<int>(
        "backend.side_effects.queue_depth", description: "Side effects found per scan");
}
