namespace Game.ClientTelemetry;

// Level by name: Error, Exception, Warning; another one drops the error, as a level out of the enum of the socket does.
public sealed class HttpTelemetryError
{
    public long Time { get; init; }
    public string? Level { get; init; }
    public string? Message { get; init; }
    public string? Stack { get; init; }
    public int Repeat { get; init; }

    // Empty or missing: none (JsonUtility writes a null string as an empty one).
    public string? Category { get; init; }
}
