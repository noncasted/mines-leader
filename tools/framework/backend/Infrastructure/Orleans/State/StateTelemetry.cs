using System.Diagnostics.Metrics;
using Common.Extensions;

namespace Infrastructure.State;

public static class StateTelemetry
{
    public static readonly Counter<long> ReadTotal = BackendMeter.Instance.CreateCounter<long>(
        "backend.state.read.total", description: "State read operations");

    public static readonly Counter<long> WriteTotal = BackendMeter.Instance.CreateCounter<long>(
        "backend.state.write.total", description: "State write operations");

    public static readonly Counter<long> DeleteTotal = BackendMeter.Instance.CreateCounter<long>(
        "backend.state.delete.total", description: "State delete operations");

    public static readonly Histogram<double> ReadDuration = BackendMeter.Instance.CreateHistogram<double>(
        "backend.state.read.duration", "ms", "State read duration");

    public static readonly Histogram<double> WriteDuration = BackendMeter.Instance.CreateHistogram<double>(
        "backend.state.write.duration", "ms", "State write duration");

    public static readonly Histogram<double> DeleteDuration = BackendMeter.Instance.CreateHistogram<double>(
        "backend.state.delete.duration", "ms", "State delete duration");

    public static readonly Histogram<int> WriteBatchSize = BackendMeter.Instance.CreateHistogram<int>(
        "backend.state.write.batch_size", description: "Records per write batch");
}
