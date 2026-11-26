namespace Game.ClientTelemetry;

// Given to the silo (the grain, the sweep) and to the hosts that read.
public sealed class ClientTelemetryStorageOptions
{
    // Both states of a session go when it was closed longer ago than this.
    public int RetentionDays { get; init; } = 7;

    // No batch for this long: the session is closed as lost. The close of the socket ends a session by itself, so this is only the
    // reserve for a gateway gone without closing its sockets: a page in the background sends rarely and keeps its session.
    public TimeSpan LostAfter { get; init; } = TimeSpan.FromSeconds(120);
    public TimeSpan LostCheckInterval { get; init; } = TimeSpan.FromSeconds(5);

    // What one session keeps in memory, the oldest goes first and is counted. 6 hours at a sample per second: 30 metrics
    // are about 10 MB. A text of an error is at most 10 KB, so the errors are at most 10 MB.
    public int MaxSamples { get; init; } = 6 * 3600;
    public int MaxErrors { get; init; } = 1000;
    public int MaxMarks { get; init; } = 10000;

    // The pass over the sessions not closed after a crash and over the expired ones.
    public TimeSpan SweepFirstDelay { get; init; } = TimeSpan.FromMinutes(1);
    public TimeSpan SweepInterval { get; init; } = TimeSpan.FromHours(1);
}
