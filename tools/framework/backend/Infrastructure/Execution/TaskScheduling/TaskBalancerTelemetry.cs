using System.Diagnostics;
using System.Diagnostics.Metrics;
using Common.Extensions;

namespace Infrastructure.Execution;

public static class TaskBalancerTelemetry
{
    public static readonly ActivitySource Source = new("Infrastructure.TaskBalancer");

    public static readonly Counter<long> Executed = BackendMeter.Instance.CreateCounter<long>(
        "backend.tasks.executed", description: "Tasks executed");

    public static readonly Counter<long> Success = BackendMeter.Instance.CreateCounter<long>(
        "backend.tasks.success", description: "Tasks succeeded");

    public static readonly Counter<long> Failure = BackendMeter.Instance.CreateCounter<long>(
        "backend.tasks.failure", description: "Tasks failed");

    public static readonly Histogram<double> Duration = BackendMeter.Instance.CreateHistogram<double>(
        "backend.tasks.duration", "ms", "Task execution duration");

    public static readonly Histogram<int> QueueDepth = BackendMeter.Instance.CreateHistogram<int>(
        "backend.tasks.queue_depth", description: "Tasks in scheduler queue");
}
