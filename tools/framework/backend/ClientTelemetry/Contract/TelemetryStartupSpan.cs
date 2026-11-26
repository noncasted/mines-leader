namespace Game.ClientTelemetry;

// ParentIndex: below its own index, -1 for a root. File: a file of the page before the build started, null for any other span.
[GenerateSerializer]
public sealed record TelemetryStartupSpan(
    int ParentIndex,
    string Name,
    double StartMs,
    double DurationMs,
    int Frames,
    bool Unfinished,
    long AllocBytes,
    string? File = null,
    long TransferBytes = 0,
    bool Cached = false);
