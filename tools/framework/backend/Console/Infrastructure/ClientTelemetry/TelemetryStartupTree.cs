using Game.ClientTelemetry;

namespace Console.Infrastructure.ClientTelemetry;

// The tree of the window of a startup trace over its spans, like the window of the editor: it opens with the roots expanded, then
// the long stages are opened by hand. The expanded spans belong to the page: a new snapshot of the trace keeps what was opened.
public sealed class TelemetryStartupTree
{
    public TelemetryStartupTree(IReadOnlyList<TelemetryStartupSpanFacts> spans, double traceDurationMs)
    {
        _tree = new SpanTree<TelemetryStartupSpanFacts>(spans, span => span.Index, span => span.ParentIndex, span => span.StartMs);

        // A span of a snapshot may end after the duration the client told: the scale holds everything.
        ScaleMs = Math.Max(1, spans.Select(span => span.StartMs + span.DurationMs).Append(traceDurationMs).Max());
    }

    private readonly SpanTree<TelemetryStartupSpanFacts> _tree;

    public IReadOnlyList<TelemetryStartupSpanFacts> Spans => _tree.Spans;
    public double ScaleMs { get; }

    // No platform but the editor measures allocations: the column stands only when there is something to show.
    public bool HasAllocations => Spans.Any(span => span.AllocBytes > 0);

    public IEnumerable<int> Roots => _tree.Roots;
    public IEnumerable<int> All => _tree.All;

    public bool HasChildren(int index)
    {
        return _tree.HasChildren(index);
    }

    public IReadOnlyList<TelemetryStartupSpanFacts> Visible(IReadOnlySet<int> expanded)
    {
        return _tree.Visible(expanded);
    }

    // The own time, not the duration: a parent holds the time of everything under it. Waiting for a player shows up here too.
    public IReadOnlyList<TelemetryStartupSpanFacts> LongestOwn(int count)
    {
        return Spans.OrderByDescending(span => span.OwnMs).ThenBy(span => span.Index).Take(count).ToArray();
    }
}
