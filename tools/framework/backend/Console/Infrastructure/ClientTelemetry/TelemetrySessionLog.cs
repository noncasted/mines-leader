using Game.ClientTelemetry;

namespace Console.Infrastructure.ClientTelemetry;

// What an open card of a session knows: the rows taken so far and the next ones after them.
public sealed class TelemetrySessionLog
{
    public TelemetrySessionLog(IClientTelemetryReader reader, Guid id)
    {
        _reader = reader;
        _id = id;
    }

    // One page of a closed session is one read of its log from the database.
    public const int PageRows = 5000;

    private readonly IClientTelemetryReader _reader;
    private readonly Guid _id;
    private readonly List<TelemetryStoredSample> _samples = [];
    private readonly List<TelemetryStoredError> _errors = [];
    private readonly List<TelemetryStoredMark> _marks = [];

    private long _sampleSeq;
    private long _errorSeq;
    private long _markSeq;
    private TelemetrySessionSummary? _liveSummary;
    private bool _changed;

    public Guid Id => _id;
    public TelemetrySessionInfo? Info { get; private set; }
    public bool IsLive => Info is { Status: TelemetrySessionStatus.Live };
    public IReadOnlyList<TelemetryStoredSample> Samples => _samples;
    public IReadOnlyList<TelemetryStoredError> Errors => _errors;
    public IReadOnlyList<TelemetryStoredMark> Marks => _marks;

    // A closed session has its digest. Without one the page counts what it holds, by the same rules: hidden samples are out.
    public TelemetrySessionSummary Summary =>
        Info?.Digest?.Summary ?? (_liveSummary ??= TelemetrySummaryCalculator.Calculate(Info?.Names ?? [], _samples));

    // True: something has changed since the last call that returned. A call that throws after taking some rows leaves
    // them to the next one: the page takes the changes only from a call that returned.
    public async Task<bool> Refresh()
    {
        var info = await _reader.GetSession(_id);
        _changed |= info?.Status != Info?.Status;

        Info = info;

        if (info == null)
            return TakeChanged();

        var sampleCount = _samples.Count;

        try
        {
            await TakeNew(_samples, () => _sampleSeq, seq => _sampleSeq = seq, _reader.GetSamples, sample => sample.Seq);
        }
        finally
        {
            // Also when a later page failed: the rows taken before it are in the log and in the order of time.
            if (_samples.Count != sampleCount)
            {
                // Seq goes by the batches of a client: nothing promises that the times of a batch follow the ones before it.
                _samples.Sort((left, right) => left.Time != right.Time ? left.Time.CompareTo(right.Time) : left.Seq.CompareTo(right.Seq));
                _liveSummary = null;
            }
        }

        await TakeNew(_errors, () => _errorSeq, seq => _errorSeq = seq, _reader.GetErrors, error => error.Seq);
        await TakeNew(_marks, () => _markSeq, seq => _markSeq = seq, _reader.GetMarks, mark => mark.Seq);

        return TakeChanged();
    }

    private bool TakeChanged()
    {
        var changed = _changed;
        _changed = false;
        return changed;
    }

    private async Task TakeNew<T>(
        List<T> rows,
        Func<long> lastSeq,
        Action<long> setLastSeq,
        Func<Guid, long, int, Task<IReadOnlyList<T>>> read,
        Func<T, long> seq)
    {
        while (true)
        {
            var page = await read(_id, lastSeq(), PageRows);

            if (page.Count == 0)
                return;

            rows.AddRange(page);
            setLastSeq(seq(page[^1]));
            _changed = true;
        }
    }
}
