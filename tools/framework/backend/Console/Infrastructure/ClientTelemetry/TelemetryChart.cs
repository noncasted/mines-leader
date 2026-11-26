using Game.ClientTelemetry;

namespace Console.Infrastructure.ClientTelemetry;

// The line of one metric. Min and Max take the hidden points too, so that they stay inside the chart.
public sealed record TelemetryChart(IReadOnlyList<TelemetryChartSegment> Segments, double Min, double Max)
{
    // A segment over buckets keeps the lowest and the highest point of each: a spike stays visible on a long session.
    public static TelemetryChart Of(IReadOnlyList<TelemetryStoredSample> samples, int slot, int buckets)
    {
        var bucketTime = samples.Count == 0 ? 1 : Math.Max(1, (samples[^1].Time - samples[0].Time + 1) / buckets);
        var segments = Runs(samples, slot)
            .Select(run => new TelemetryChartSegment(run.Hidden, Reduce(run.Points, bucketTime)))
            .ToArray();
        var values = segments.SelectMany(segment => segment.Points).Select(point => point.Value).ToArray();

        return new TelemetryChart(segments, values.DefaultIfEmpty().Min(), values.DefaultIfEmpty().Max());
    }

    private static IEnumerable<(bool Hidden, List<TelemetryChartPoint> Points)> Runs(IReadOnlyList<TelemetryStoredSample> samples, int slot)
    {
        List<TelemetryChartPoint>? run = null;
        var hidden = false;

        foreach (var sample in samples)
        {
            var value = slot < sample.Values.Count ? sample.Values[slot] : null;

            if (run != null && (value == null || sample.Hidden != hidden))
            {
                yield return (hidden, run);
                run = null;
            }

            if (value == null)
                continue;

            hidden = sample.Hidden;
            run ??= [];
            run.Add(new TelemetryChartPoint(sample.Time, value.Value));
        }

        if (run != null)
            yield return (hidden, run);
    }

    private static IReadOnlyList<TelemetryChartPoint> Reduce(List<TelemetryChartPoint> points, long bucketTime)
    {
        var reduced = points
            .GroupBy(point => point.Time / bucketTime)
            .SelectMany(bucket => new[] { bucket.MinBy(point => point.Value)!, bucket.MaxBy(point => point.Value)! });

        return reduced.Append(points[0]).Append(points[^1]).Distinct().OrderBy(point => point.Time).ToArray();
    }
}
