namespace Game.ClientTelemetry;

// POST <group>/batch.
public sealed class HttpTelemetryBatch
{
    public Guid LinkId { get; init; }
    public Guid SessionId { get; init; }
    public HttpTelemetryName[]? Names { get; init; }
    public HttpTelemetrySample[]? Samples { get; init; }
    public HttpTelemetryError[]? Errors { get; init; }
    public HttpTelemetryMark[]? Marks { get; init; }
}
