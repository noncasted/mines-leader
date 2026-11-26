using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Common.Extensions;

public struct MetricWatch : IDisposable
{
    private readonly Histogram<double> _histogram;
    private readonly long _start;

    private MetricWatch(Histogram<double> histogram)
    {
        _histogram = histogram;
        _start = Stopwatch.GetTimestamp();
    }

    public static MetricWatch Start(Histogram<double> histogram) => new(histogram);

    public void Dispose()
    {
        _histogram.Record(Stopwatch.GetElapsedTime(_start).TotalMilliseconds);
    }
}
