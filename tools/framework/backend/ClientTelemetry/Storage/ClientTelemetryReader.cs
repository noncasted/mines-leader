using Infrastructure;

namespace Game.ClientTelemetry;

// A live session answers from the memory of its grain, without the database. A closed one answers from its states here, on the
// host that reads: its log is never loaded into a grain.
internal sealed class ClientTelemetryReader : IClientTelemetryReader
{
    public ClientTelemetryReader(ITelemetrySessionCollection sessions, TelemetrySessionStore store, TelemetryStartupStore startups, IOrleans orleans)
    {
        _sessions = sessions;
        _store = store;
        _startups = startups;
        _orleans = orleans;
    }

    private readonly ITelemetrySessionCollection _sessions;
    private readonly TelemetrySessionStore _store;
    private readonly TelemetryStartupStore _startups;
    private readonly IOrleans _orleans;

    public Task<IReadOnlyList<TelemetrySessionInfo>> GetSessions(TelemetrySessionFilter filter)
    {
        IReadOnlyList<TelemetrySessionInfo> page = _sessions.Values
            .Select(session => session.ToInfo())
            .Where(filter.Matches)
            .OrderByDescending(session => session.StartedAt)
            .ThenBy(session => session.Id)
            .Skip(filter.Offset)
            .Take(filter.Limit)
            .ToArray();

        return Task.FromResult(page);
    }

    public Task<TelemetryGlobalErrorGroups> GetErrorGroups(int limit)
    {
        return Task.FromResult(TelemetryGlobalErrorGroups.Of(_sessions.Values.Select(session => session.ToInfo()), limit));
    }

    public async Task<TelemetrySessionInfo?> GetSession(Guid id)
    {
        if (await _store.Find(id) is not { } session)
            return null;

        return session.Closed ? session.ToInfo() : await Grain(id).GetInfo();
    }

    public Task<IReadOnlyList<TelemetryStoredSample>> GetSamples(Guid session, long afterSeq, int limit)
    {
        return ReadPage(
            session,
            grain => grain.GetSamples(afterSeq, limit),
            log => TelemetryRows.Page(log.Samples, afterSeq, limit, sample => sample.Seq));
    }

    public Task<IReadOnlyList<TelemetryStoredError>> GetErrors(Guid session, long afterSeq, int limit)
    {
        return ReadPage(
            session,
            grain => grain.GetErrors(afterSeq, limit),
            log => TelemetryRows.Page(log.Errors, afterSeq, limit, error => error.Seq));
    }

    public Task<IReadOnlyList<TelemetryStoredMark>> GetMarks(Guid session, long afterSeq, int limit)
    {
        return ReadPage(
            session,
            grain => grain.GetMarks(afterSeq, limit),
            log => TelemetryRows.Page(log.Marks, afterSeq, limit, mark => mark.Seq));
    }

    public Task<IReadOnlyList<TelemetryStoredSample>> GetSampleWindow(Guid session, long from, long to)
    {
        return ReadPage(
            session,
            grain => grain.GetSampleWindow(from, to),
            log => TelemetryRows.Window(log.Samples, from, to));
    }

    public Task<IReadOnlyList<TelemetryStartupInfo>> GetStartups(int limit)
    {
        return Task.FromResult(_startups.List(limit));
    }

    public async Task<TelemetryStartupInfo?> GetStartup(Guid id)
    {
        return (await _startups.Find(id))?.ToInfo();
    }

    public Task<IReadOnlyList<TelemetryStartupSpan>> GetStartupSpans(Guid id)
    {
        return _startups.ReadSpans(id);
    }

    private async Task<IReadOnlyList<T>> ReadPage<T>(
        Guid id,
        Func<IClientTelemetrySession, Task<IReadOnlyList<T>?>> live,
        Func<TelemetrySessionLogState, IReadOnlyList<T>> closed)
    {
        if (await _store.Find(id) is not { } session)
            return [];

        // Null from the grain: it has closed while the collection of this host did not know yet.
        if (!session.Closed && await live(Grain(id)) is { } rows)
            return rows;

        return closed(await _store.ReadLog(id));
    }

    private IClientTelemetrySession Grain(Guid id)
    {
        return _orleans.GetGrain<IClientTelemetrySession>(id);
    }
}
