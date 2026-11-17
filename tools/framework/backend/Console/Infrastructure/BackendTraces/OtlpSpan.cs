namespace Console.Infrastructure.BackendTraces;

// A span as the OTLP JSON tells it, before the tree: ids and times of the wire, the service of its resource.
public sealed record OtlpSpan
{
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
