using System.Diagnostics;
using System.Diagnostics.Metrics;
using Common.Extensions;

namespace Infrastructure;

public static class DurableQueueTelemetry
{
    public static readonly ActivitySource Source = new("Infrastructure.Messaging.DurableQueue");

    public static readonly Counter<long> Pushed = BackendMeter.Instance.CreateCounter<long>(
        "backend.durable_queue.pushed", description: "Messages pushed to durable queue");

    public static readonly Histogram<int> ObserverCount = BackendMeter.Instance.CreateHistogram<int>(
        "backend.durable_queue.observer_count", description: "Observers at push time");

    public static readonly Counter<long> DeliveryFailure = BackendMeter.Instance.CreateCounter<long>(
        "backend.durable_queue.delivery_failure", description: "Failed deliveries");

    public static readonly Counter<long> NoSubscribers = BackendMeter.Instance.CreateCounter<long>(
        "backend.durable_queue.no_subscribers", description: "Push with no active subscribers");
}
