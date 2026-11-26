using System.Diagnostics;
using System.Diagnostics.Metrics;
using Common.Extensions;

namespace Infrastructure;

public static class RuntimeChannelTelemetry
{
    public static readonly ActivitySource Source = new("Infrastructure.Messaging.RuntimeChannel");

    public static readonly Counter<long> Published = BackendMeter.Instance.CreateCounter<long>(
        "backend.channel.published", description: "Messages published to channel");

    public static readonly Histogram<int> ObserverCount = BackendMeter.Instance.CreateHistogram<int>(
        "backend.channel.observer_count", description: "Observers at publish time");

    public static readonly Counter<long> DeliveryFailure = BackendMeter.Instance.CreateCounter<long>(
        "backend.channel.delivery_failure", description: "Failed deliveries");

    public static readonly Counter<long> DeliveryTimeout = BackendMeter.Instance.CreateCounter<long>(
        "backend.channel.delivery_timeout", description: "Observer delivery timeouts");

    public static readonly Counter<long> CatchUpExecuted = BackendMeter.Instance.CreateCounter<long>(
        "backend.channel.catchup_executed", description: "Catch-up operations executed");

    public static readonly Histogram<int> CatchUpMessages = BackendMeter.Instance.CreateHistogram<int>(
        "backend.channel.catchup_messages", description: "Messages delivered per catch-up");

    public static readonly Counter<long> GapDetected = BackendMeter.Instance.CreateCounter<long>(
        "backend.channel.gap_detected", description: "Gaps detected during catch-up");
}
