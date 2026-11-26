namespace Game.ClientTelemetry;

// Over a tree checked by the parser: the parent of a span is before it.
public static class TelemetryStartupAnalysis
{
    private const int _timeDecimals = 3;

    public static IReadOnlyList<TelemetryStartupSpanFacts> Of(IReadOnlyList<TelemetryStartupSpan> spans)
    {
        var depths = Depths(spans);
        var children = new List<TelemetryStartupSpan>?[spans.Count];

        foreach (var span in spans.Where(span => span.ParentIndex >= 0))
            (children[span.ParentIndex] ??= []).Add(span);

        return spans.Select((span, index) => new TelemetryStartupSpanFacts(
                        index,
                        span.ParentIndex,
                        depths[index],
                        span.Name,
                        span.StartMs,
                        span.DurationMs,
                        Math.Round(span.DurationMs - Covered(span, children[index]), _timeDecimals),
                        span.Frames,
                        span.Unfinished,
                        span.AllocBytes,
                        span.File,
                        span.TransferBytes,
                        span.Cached))
                    .ToArray();
    }

    public static int Depth(IReadOnlyList<TelemetryStartupSpan> spans)
    {
        return spans.Count == 0 ? 0 : Depths(spans).Max() + 1;
    }

    private static int[] Depths(IReadOnlyList<TelemetryStartupSpan> spans)
    {
        var depths = new int[spans.Count];

        for (var index = 0; index < spans.Count; index++)
            depths[index] = spans[index].ParentIndex < 0 ? 0 : depths[spans[index].ParentIndex] + 1;

        return depths;
    }

    // The union of the children inside their parent: a concurrent span (a request) runs next to its siblings, so a sum may exceed the parent.
    private static double Covered(TelemetryStartupSpan parent, List<TelemetryStartupSpan>? children)
    {
        if (children == null)
            return 0;

        var parentEnd = parent.StartMs + parent.DurationMs;
        var covered = 0d;
        var reached = parent.StartMs;

        foreach (var child in children.OrderBy(child => child.StartMs))
        {
            var from = Math.Max(child.StartMs, reached);
            var to = Math.Min(child.StartMs + child.DurationMs, parentEnd);

            if (to <= from)
                continue;

            covered += to - from;
            reached = to;
        }

        return covered;
    }
}
