namespace Game.ClientTelemetry;

public interface IClientTelemetryReader
{
    // From the newest start, over the compact states in memory: no database and no grain.
    Task<IReadOnlyList<TelemetrySessionInfo>> GetSessions(TelemetrySessionFilter filter);

    // The kinds of errors over the closed sessions, over the compact states in memory: a live session has no digest yet.
    Task<TelemetryGlobalErrorGroups> GetErrorGroups(int limit);

    // Null: no such session.
    Task<TelemetrySessionInfo?> GetSession(Guid id);

    // The pages go by seq, in the order of seq and time: the next page starts after the Seq of the last row. A limit never
    // cuts one seq, so a page may hold more rows than the limit (up to the limits of one batch).
    Task<IReadOnlyList<TelemetryStoredSample>> GetSamples(Guid session, long afterSeq, int limit);
    Task<IReadOnlyList<TelemetryStoredError>> GetErrors(Guid session, long afterSeq, int limit);
    Task<IReadOnlyList<TelemetryStoredMark>> GetMarks(Guid session, long afterSeq, int limit);

    // The samples with the time of the session from from to to, inclusive, in the order of seq: a closed session reads its log once.
    Task<IReadOnlyList<TelemetryStoredSample>> GetSampleWindow(Guid session, long from, long to);

    // The startup traces: the newest first over the compact states in memory; null or empty for no such record.
    Task<IReadOnlyList<TelemetryStartupInfo>> GetStartups(int limit);
    Task<TelemetryStartupInfo?> GetStartup(Guid id);
    Task<IReadOnlyList<TelemetryStartupSpan>> GetStartupSpans(Guid id);
}
