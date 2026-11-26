namespace Game.ClientTelemetry;

public static class TelemetrySummaryCalculator
{
    private const string _bytesUnit = "bytes";

    // At the close of a session: the duration is the latest time of the session, whatever part of its rows the memory has kept.
    public static TelemetrySessionDigest Digest(TelemetrySessionBuffer buffer)
    {
        var duration = new[]
        {
            buffer.Samples.Items.LastOrDefault()?.Time ?? 0,
            buffer.Errors.Items.LastOrDefault()?.Time ?? 0,
            buffer.Marks.Items.LastOrDefault()?.Time ?? 0
        }.Max();

        return new TelemetrySessionDigest(
            duration,
            buffer.Errors.Items.Count(error => error.Level != TelemetryErrorLevel.Warning),
            buffer.Errors.Items.Where(error => error.Level != TelemetryErrorLevel.Warning).Sum(error => (long)error.Repeat),
            buffer.Marks.Items.Count,
            buffer.Samples.Dropped,
            buffer.Errors.Dropped,
            buffer.Marks.Dropped,
            buffer.RejectedRequests,
            Calculate(buffer.Names, buffer.Samples.Items),
            TelemetrySessionErrorGroups.Of(buffer.Errors.Items),
            buffer.Errors.Items.Where(error => error.Level == TelemetryErrorLevel.Warning).Sum(error => (long)error.Repeat));
    }

    // The samples in the order of time: the growth takes the first and the last value.
    public static TelemetrySessionSummary Calculate(IReadOnlyList<TelemetryMetricName> names, IReadOnlyList<TelemetryStoredSample> samples)
    {
        var visible = samples.Where(sample => !sample.Hidden).ToArray();
        var metrics = names.Select(name => Calculate(name, visible)).OfType<TelemetryMetricSummary>().ToArray();

        return new TelemetrySessionSummary(visible.Length, samples.Count - visible.Length, metrics);
    }

    private static TelemetryMetricSummary? Calculate(TelemetryMetricName name, IReadOnlyList<TelemetryStoredSample> samples)
    {
        var values = samples
            .Where(sample => name.Slot < sample.Values.Count)
            .Select(sample => sample.Values[name.Slot])
            .OfType<double>()
            .ToArray();

        if (values.Length == 0)
            return null;

        var sorted = values.Order().ToArray();

        return new TelemetryMetricSummary(
            name.Name,
            name.Unit,
            values.Length,
            Average(values),
            Percentile(sorted, 0.5),
            Percentile(sorted, 0.95),
            sorted[0],
            sorted[^1],
            name.Unit == _bytesUnit ? Growth(values) : null);
    }

    // The values come from a client: near the limit of a double a sum overflows, and a number that is not finite has no JSON, so
    // the row of the session could not be read any more, nor the list around it.
    private static double Average(double[] values)
    {
        var sum = values.Sum();

        return double.IsFinite(sum) ? sum / values.Length : values.Sum(value => value / values.Length);
    }

    private static double? Growth(double[] values)
    {
        var growth = values[^1] - values[0];

        return double.IsFinite(growth) ? growth : null;
    }

    // The nearest rank: a value that was really seen, a spike is not averaged with its neighbour.
    private static double Percentile(double[] sorted, double share)
    {
        return sorted[(int)Math.Ceiling(share * sorted.Length) - 1];
    }
}
