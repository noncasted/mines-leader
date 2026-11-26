namespace Game.ClientTelemetry;

// POST <group>/startup.
public sealed class HttpTelemetryStartupTrace
{
    public Guid LinkId { get; init; }
    public Guid StartupId { get; init; }
    public HttpTelemetryDevice? Device { get; init; }
    public string? Name { get; init; }
    public bool Final { get; init; }
    public double DurationMs { get; init; }
    public int Frames { get; init; }
    public HttpTelemetryStartupSpan[]? Spans { get; init; }
}
