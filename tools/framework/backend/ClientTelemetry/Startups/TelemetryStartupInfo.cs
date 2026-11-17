namespace Game.ClientTelemetry;

public sealed record TelemetryStartupInfo
{
    public required Guid Id { get; init; }
    public required TelemetryDevice Device { get; init; }
    public required string Name { get; init; }

    // False: a snapshot of a trace still open when it was sent. It stays so when the socket goes.
    public required bool Final { get; init; }

    public required DateTime ReceivedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
    public required double DurationMs { get; init; }
    public required int Frames { get; init; }
    public required int SpanCount { get; init; }
    public required int Depth { get; init; }
    public required int UnfinishedSpans { get; init; }
    public required int DroppedSpans { get; init; }
    public required int RejectedSpans { get; init; }
    public required int CutNames { get; init; }

    // The trace of the authorization of the socket in the OTLP backend (Aspire dashboard: Traces). Null: the game passes none.
    public required string? BackendTraceId { get; init; }
}
