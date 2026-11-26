namespace Console.Infrastructure.BackendTraces;

// The spans of a store as a tree on the own axis of the trace: indices by start, parents by span id, depths, offsets.
public static class BackendTraceAssembler
{
    private const double _nanosPerMs = 1_000_000;
    private const long _nanosPerTick = 100;

    public static BackendTrace Assemble(string traceId, IReadOnlyList<BackendTraceWireSpan> spans)
    {
        var ordered = spans.OrderBy(span => span.StartUnixNano).ThenBy(span => span.SpanId, StringComparer.Ordinal).ToArray();
        var indices = new Dictionary<string, int>(ordered.Length);

        for (var index = 0; index < ordered.Length; index++)
            indices.Add(ordered[index].SpanId, index);

        var parents = ordered.Select(span => Parent(span, indices)).ToArray();
        var depths = Depths(parents);
        var start = ordered[0].StartUnixNano;
        var end = ordered.Max(span => span.EndUnixNano);

        return new BackendTrace
        {
            TraceId = traceId,
            StartUtc = Utc(start),
            DurationMs = Ms(start, end),
            Spans = ordered.Select((span, index) => new BackendTraceSpan
            {
                Index = index,
                ParentIndex = parents[index],
                Depth = depths[index],
                SpanId = span.SpanId,
                Name = span.Name,
                Service = span.Service,
                Scope = span.Scope,
                StartUtc = Utc(span.StartUnixNano),
                EndUtc = Utc(span.EndUnixNano),
                StartMs = Ms(start, span.StartUnixNano),
                DurationMs = Ms(span.StartUnixNano, span.EndUnixNano),
                Attributes = span.Attributes,
                Error = span.Error
            }).ToArray()
        };
    }

    private static int Parent(BackendTraceWireSpan span, IReadOnlyDictionary<string, int> indices)
    {
        return span.ParentSpanId != null && indices.TryGetValue(span.ParentSpanId, out var parent) ? parent : SpanTree<BackendTraceSpan>.NoParent;
    }

    // Walks up to a known depth; a cycle of parents is no trace.
    private static int[] Depths(IReadOnlyList<int> parents)
    {
        var depths = Enumerable.Repeat(-1, parents.Count).ToArray();

        for (var index = 0; index < parents.Count; index++)
        {
            var path = new Stack<int>();
            var current = index;

            while (current != SpanTree<BackendTraceSpan>.NoParent && depths[current] < 0)
            {
                if (path.Count > parents.Count)
                    throw new InvalidOperationException($"The parents of the span {index} make a cycle");

                path.Push(current);
                current = parents[current];
            }

            var depth = current == SpanTree<BackendTraceSpan>.NoParent ? -1 : depths[current];

            while (path.TryPop(out var node))
                depths[node] = ++depth;
        }

        return depths;
    }

    private static DateTime Utc(ulong unixNano)
    {
        return DateTime.UnixEpoch.AddTicks((long)(unixNano / _nanosPerTick));
    }

    // An end before its start (a clock of another host) is a zero duration.
    private static double Ms(ulong from, ulong to)
    {
        return to > from ? (to - from) / _nanosPerMs : 0;
    }
}
