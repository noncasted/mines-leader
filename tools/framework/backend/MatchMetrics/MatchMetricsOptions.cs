namespace Game.MatchMetrics;

// Built by the game, or the defaults.
public sealed class MatchMetricsOptions
{
    // The answer of the admission is asked again by a stream that took it longer ago than this: a match closed meanwhile.
    public TimeSpan AdmissionRecheck { get; init; } = TimeSpan.FromSeconds(60);

    // The batches of a closed match taken this long after its close: the last batch of a run and the batches buffered over
    // a reconnect at its end.
    public TimeSpan CloseGrace { get; init; } = TimeSpan.FromMinutes(2);

    // The real time between the writes of a stream of the server.
    public TimeSpan ServerFlush { get; init; } = TimeSpan.FromSeconds(30);

    // The pause between the tries of the last write of a stream of the server that failed; they go on for CloseGrace.
    public TimeSpan ServerLastWriteRetry { get; init; } = TimeSpan.FromSeconds(5);
}
