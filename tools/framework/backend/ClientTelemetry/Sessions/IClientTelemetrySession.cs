namespace Game.ClientTelemetry;

// One tagged part of the game of a client, its guid issued by the gateway. The only writer of the session: the slots of names and
// the seq of the rows are decided here, one at a time.
public interface IClientTelemetrySession : IGrainWithGuidKey
{
    // StartupId: the record of the startup trace of the same socket, checked by its connection. Previous: what the client reports of
    // its session before this one that did not close.
    Task Start(TelemetryDevice device, TelemetrySessionTags tags, Guid? startupId, TelemetryPreviousSession? previous);

    // False: the session is not live (closed, or its data went down with a silo), the client stops.
    Task<bool> Accept(TelemetryBatch batch);

    // With the summary and the log. A session that is not live stays as it is. Throws when the write fails: the timer repeats it.
    Task Close(TelemetrySessionStatus status);

    // Null: no such session.
    Task<TelemetrySessionInfo?> GetInfo();

    // From the memory. Null: the session is not live, its rows are in the state of the log. A page goes by seq and a limit never
    // cuts one seq, so it may hold more rows than the limit (up to the limits of one batch).
    Task<IReadOnlyList<TelemetryStoredSample>?> GetSamples(long afterSeq, int limit);
    Task<IReadOnlyList<TelemetryStoredError>?> GetErrors(long afterSeq, int limit);
    Task<IReadOnlyList<TelemetryStoredMark>?> GetMarks(long afterSeq, int limit);

    // From the memory, the samples with the time from from to to, inclusive. Null: the session is not live.
    Task<IReadOnlyList<TelemetryStoredSample>?> GetSampleWindow(long from, long to);

    // The pass of ClientTelemetrySweep: the activation itself closes what a crash left open, an expired session is deleted.
    Task Sweep();
}
