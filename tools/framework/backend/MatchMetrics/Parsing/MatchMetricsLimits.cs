namespace Game.MatchMetrics;

// The sizes of a batch a client may send, the server's alone (the names, tags and fields: SharedMatchMetricsLimits): a wide
// margin over a batch of 30 s with a buffer of ten minutes.
internal static class MatchMetricsLimits
{
    public const int MaxChunks = 4096;
    public const int MaxValues = 65536;
    public const int MaxEvents = 1024;

    // A bucket is a second of the clock of the run: a day of a run is far past any match.
    public const int MaxBucket = 86400;

    // A batch older than a day is not a reconnect.
    public const long MaxAgeMs = 86_400_000;
}
