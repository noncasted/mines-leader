using Game.ClientTelemetry;

namespace Framework.Tests.ClientTelemetry;

// The pages of IClientTelemetryReader over lists: a limit never cuts one seq, like the real one.
public sealed class FakeTelemetryReader : IClientTelemetryReader
{
    private readonly Dictionary<Guid, TelemetrySessionInfo> _sessions = new();
    private readonly Queue<Exception> _errorReadFailures = new();
    private readonly List<TelemetryStoredSample> _samples = [];
    private readonly List<TelemetryStoredMark> _marks = [];
    private readonly Dictionary<Guid, TelemetryStoredError[]> _errors = new();
    private readonly List<Guid> _errorReads = [];

    // The sessions whose errors were read, in the order of the reads.
    public IReadOnlyList<Guid> ErrorReads => _errorReads;

    public void Set(TelemetrySessionInfo session)
    {
        _sessions[session.Id] = session;
    }

    public void AddSamples(params TelemetryStoredSample[] samples)
    {
        _samples.AddRange(samples);
    }

    public void AddMarks(params TelemetryStoredMark[] marks)
    {
        _marks.AddRange(marks);
    }

    public void SetErrors(Guid session, params TelemetryStoredError[] errors)
    {
        _errors[session] = errors;
    }

    // The next read of the errors throws, like a database that went away in the middle of a refresh.
    public void FailNextErrorRead(Exception exception)
    {
        _errorReadFailures.Enqueue(exception);
    }

    public Task<TelemetrySessionInfo?> GetSession(Guid id)
    {
        return Task.FromResult(_sessions.GetValueOrDefault(id));
    }

    public Task<IReadOnlyList<TelemetryStoredSample>> GetSamples(Guid session, long afterSeq, int limit)
    {
        return Task.FromResult(Page(_samples, afterSeq, limit, sample => sample.Seq));
    }

    public Task<IReadOnlyList<TelemetryStoredError>> GetErrors(Guid session, long afterSeq, int limit)
    {
        _errorReads.Add(session);

        if (_errorReadFailures.TryDequeue(out var failure))
            return Task.FromException<IReadOnlyList<TelemetryStoredError>>(failure);

        return Task.FromResult(Page(_errors.GetValueOrDefault(session) ?? [], afterSeq, limit, error => error.Seq));
    }

    public Task<IReadOnlyList<TelemetryStoredMark>> GetMarks(Guid session, long afterSeq, int limit)
    {
        return Task.FromResult(Page(_marks, afterSeq, limit, mark => mark.Seq));
    }

    public Task<IReadOnlyList<TelemetryStoredSample>> GetSampleWindow(Guid session, long from, long to)
    {
        return Task.FromResult<IReadOnlyList<TelemetryStoredSample>>(_samples.Where(sample => sample.Time >= from && sample.Time <= to).ToArray());
    }

    public Task<IReadOnlyList<TelemetrySessionInfo>> GetSessions(TelemetrySessionFilter filter)
    {
        throw new NotSupportedException();
    }

    public Task<IReadOnlyList<TelemetryStartupInfo>> GetStartups(int limit)
    {
        throw new NotSupportedException();
    }

    public Task<TelemetryStartupInfo?> GetStartup(Guid id)
    {
        throw new NotSupportedException();
    }

    public Task<IReadOnlyList<TelemetryStartupSpan>> GetStartupSpans(Guid id)
    {
        throw new NotSupportedException();
    }

    public Task<TelemetryGlobalErrorGroups> GetErrorGroups(int limit)
    {
        throw new NotSupportedException();
    }

    private static IReadOnlyList<T> Page<T>(IEnumerable<T> rows, long afterSeq, int limit, Func<T, long> seq)
    {
        var next = rows.Where(row => seq(row) > afterSeq).ToArray();

        if (next.Length <= limit)
            return next;

        return next.Where(row => seq(row) <= seq(next[limit - 1])).ToArray();
    }
}
