namespace Game.ClientTelemetry;

public sealed class HttpTelemetryPreviousSession
{
    public Guid SessionId { get; init; }
    public string[]? Tags { get; init; }
    public long LastSampleTime { get; init; }
    public long SinceMs { get; init; }
    public HttpTelemetryMark[]? Marks { get; init; }
    public string[]? SampleNames { get; init; }
    public HttpTelemetrySample[]? Samples { get; init; }
}
