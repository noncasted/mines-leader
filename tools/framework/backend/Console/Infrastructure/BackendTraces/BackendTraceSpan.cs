namespace Console.Infrastructure.BackendTraces;

// One span of a backend trace. ParentIndex is SpanTree<T>.NoParent for a root: a span without a parent or with a parent outside
// the trace (a link from another service that has not sent it). StartMs is the offset from the start of the trace.
public sealed record BackendTraceSpan
{
    public required int Index { get; init; }
    public required int ParentIndex { get; init; }
    public required int Depth { get; init; }
    public required string SpanId { get; init; }
    public required string Name { get; init; }
    public required string Service { get; init; }
    public required string Scope { get; init; }
    public required DateTime StartUtc { get; init; }
    public required DateTime EndUtc { get; init; }
    public required double StartMs { get; init; }
    public required double DurationMs { get; init; }
    public required IReadOnlyList<KeyValuePair<string, string>> Attributes { get; init; }
    public required BackendTraceError? Error { get; init; }
}
