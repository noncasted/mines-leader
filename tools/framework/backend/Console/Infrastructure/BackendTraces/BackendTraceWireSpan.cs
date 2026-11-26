namespace Console.Infrastructure.BackendTraces;

// A span as a trace store tells it, before the tree: ids of the wire, times in unix nanoseconds, the service of its resource.
// Every source parses its answer into these; BackendTraceAssembler builds the tree once for all of them.
public sealed record BackendTraceWireSpan
{
    // The service of a span whose resource has no service.name.
    public const string UnknownService = "unknown";

    public required string SpanId { get; init; }
    public required string? ParentSpanId { get; init; }
    public required string Name { get; init; }
    public required string Service { get; init; }
    public required string Scope { get; init; }
    public required ulong StartUnixNano { get; init; }
    public required ulong EndUnixNano { get; init; }
    public required IReadOnlyList<KeyValuePair<string, string>> Attributes { get; init; }
    public required BackendTraceError? Error { get; init; }
}
