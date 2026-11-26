namespace Console.Infrastructure.BackendTraces;

// The spans of one trace by their start, on the own axis of the trace: its first start is zero.
public sealed record BackendTrace
{
    public required string TraceId { get; init; }
    public required DateTime StartUtc { get; init; }
    public required double DurationMs { get; init; }
    public required IReadOnlyList<BackendTraceSpan> Spans { get; init; }
}
