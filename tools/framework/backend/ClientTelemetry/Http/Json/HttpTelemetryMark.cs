namespace Game.ClientTelemetry;

// An empty detail is none; HasDuration false: the mark closes nothing.
public sealed class HttpTelemetryMark
{
    public long Time { get; init; }
    public string? Name { get; init; }
    public string? Detail { get; init; }
    public bool HasDuration { get; init; }
    public long DurationMs { get; init; }
}
