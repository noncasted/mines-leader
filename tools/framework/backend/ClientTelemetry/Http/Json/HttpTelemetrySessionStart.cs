namespace Game.ClientTelemetry;

// POST <group>/start. The JSON of the client (JsonUtility writes no null: HasPrevious says whether Previous is there).
public sealed class HttpTelemetrySessionStart
{
    public Guid LinkId { get; init; }
    public HttpTelemetryDevice? Device { get; init; }
    public string[]? Tags { get; init; }
    public Guid StartupId { get; init; }
    public bool HasPrevious { get; init; }
    public HttpTelemetryPreviousSession? Previous { get; init; }
}
