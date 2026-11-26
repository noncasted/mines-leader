namespace Game.ClientTelemetry;

// A startup trace after TelemetryStartupParser.
[GenerateSerializer]
public sealed record TelemetryStartupTrace
{
    [Id(0)] public required TelemetryDevice Device { get; init; }
    [Id(1)] public required string Name { get; init; }
    [Id(2)] public required bool Final { get; init; }
    [Id(3)] public required double DurationMs { get; init; }
    [Id(4)] public required int Frames { get; init; }
    [Id(5)] public required IReadOnlyList<TelemetryStartupSpan> Spans { get; init; }

    // Over the limits: the tail is cut, the parents of what stays are all there.
    [Id(6)] public required int DroppedSpans { get; init; }

    // Values that broke a rule: a span goes with its children, a number of the trace reads as 0.
    [Id(7)] public required int RejectedSpans { get; init; }

    [Id(8)] public required int CutNames { get; init; }

    // Not of the client: the trace of the authorization of its socket in the OTLP backend, set by the gateway. Null: the game has none.
    [Id(9)] public string? BackendTraceId { get; init; }
}
