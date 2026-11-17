using System.Diagnostics;
using System.Diagnostics.Metrics;
using Common.Extensions;

namespace Infrastructure;

public static class RuntimePipeTelemetry
{
    public static readonly ActivitySource Source = new("Infrastructure.Messaging.RuntimePipe");

    public static readonly Counter<long> RequestSent = BackendMeter.Instance.CreateCounter<long>(
        "backend.pipe.sent", description: "Pipe requests sent");

    public static readonly Counter<long> Timeout = BackendMeter.Instance.CreateCounter<long>(
        "backend.pipe.timeout", description: "Pipe request timeouts");

    public static readonly Counter<long> Retry = BackendMeter.Instance.CreateCounter<long>(
        "backend.pipe.retry", description: "Pipe send retries");

    public static readonly Histogram<double> Duration = BackendMeter.Instance.CreateHistogram<double>(
        "backend.pipe.duration", "ms", "Pipe request-response duration");
}
