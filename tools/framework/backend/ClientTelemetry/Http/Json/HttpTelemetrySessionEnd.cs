namespace Game.ClientTelemetry;

// POST <group>/end.
public sealed class HttpTelemetrySessionEnd
{
    public Guid LinkId { get; init; }
    public Guid SessionId { get; init; }
}
