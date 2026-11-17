using Game.ClientTelemetry;

namespace Console.Infrastructure.ClientTelemetry;

// The scale shared by the charts of a session: from 0 to its latest row of any kind. Events are joined by buckets: a session may
// hold ten thousand marks, each chart draws every tick.
public sealed record TelemetryTimeline(long End, IReadOnlyList<TelemetryTimelineTick> Ticks)
{
    private const int _labelEvents = 3;
    private const int _labelTextLength = 120;

    public static TelemetryTimeline Of(
        IReadOnlyList<TelemetryStoredSample> samples,
        IReadOnlyList<TelemetryStoredError> errors,
        IReadOnlyList<TelemetryStoredMark> marks,
        int buckets)
    {
        var latest = samples.Select(sample => sample.Time)
                            .Concat(errors.Select(error => error.Time))
                            .Concat(marks.Select(mark => mark.Time))
                            .DefaultIfEmpty()
                            .Max();
        var end = Math.Max(1, latest);
        var bucketTime = Math.Max(1, end / buckets);

        var markTicks = marks
            .GroupBy(mark => mark.Time / bucketTime)
            .Select(bucket => Tick(TelemetryTimelineKind.Mark, bucket.Select(mark => (mark.Time, 1L, mark.Name))));
        var errorTicks = errors
            .GroupBy(error => error.Time / bucketTime)
            .Select(bucket => Tick(TelemetryTimelineKind.Error, bucket.Select(error => (error.Time, (long)error.Repeat, $"{error.Level}: {error.Message}"))));

        return new TelemetryTimeline(end, markTicks.Concat(errorTicks).OrderBy(tick => tick.Time).ToArray());
    }

    public IReadOnlyList<long> Axis(int parts)
    {
        return Enumerable.Range(0, parts + 1).Select(part => End * part / parts).ToArray();
    }

    private static TelemetryTimelineTick Tick(TelemetryTimelineKind kind, IEnumerable<(long Time, long Count, string Text)> events)
    {
        var all = events.ToArray();
        var label = string.Join("\n", all.Take(_labelEvents).Select(item => Cut(item.Text)));

        if (all.Length > _labelEvents)
            label += $"\n+{all.Length - _labelEvents}";

        return new TelemetryTimelineTick(all.Min(item => item.Time), kind, all.Sum(item => item.Count), label);
    }

    private static string Cut(string text)
    {
        return text.Length <= _labelTextLength ? text : text[.._labelTextLength];
    }
}
