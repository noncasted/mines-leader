namespace Game.ClientTelemetry;

// A span with what its tree tells. OwnMs: the duration without the time covered by its children. File, TransferBytes, Cached: a
// file of the page, see TelemetryStartupSpan.
public sealed record TelemetryStartupSpanFacts(
    int Index,
    int ParentIndex,
    int Depth,
    string Name,
    double StartMs,
    double DurationMs,
    double OwnMs,
    int Frames,
    bool Unfinished,
    long AllocBytes,
    string? File,
    long TransferBytes,
    bool Cached);
