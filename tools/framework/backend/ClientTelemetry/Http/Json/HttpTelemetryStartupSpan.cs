namespace Game.ClientTelemetry;

// An empty file: not a file of the page.
public sealed class HttpTelemetryStartupSpan
{
    public int ParentIndex { get; init; }
    public string? Name { get; init; }
    public double StartMs { get; init; }
    public double DurationMs { get; init; }
    public int Frames { get; init; }
    public bool Unfinished { get; init; }
    public long AllocBytes { get; init; }
    public string? File { get; init; }
    public long TransferBytes { get; init; }
    public bool Cached { get; init; }
}
