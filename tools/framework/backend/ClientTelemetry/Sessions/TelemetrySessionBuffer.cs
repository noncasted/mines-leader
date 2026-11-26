namespace Game.ClientTelemetry;

// A live session in the memory of its grain.
public sealed class TelemetrySessionBuffer
{
    private TelemetrySessionBuffer(ClientTelemetryStorageOptions options, long rejected)
    {
        RejectedRequests = rejected;
        Samples = new TelemetryBoundedRows<TelemetryStoredSample>(options.MaxSamples);
        Errors = new TelemetryBoundedRows<TelemetryStoredError>(options.MaxErrors);
        Marks = new TelemetryBoundedRows<TelemetryStoredMark>(options.MaxMarks);
    }

    private readonly List<string?> _names = [];
    private readonly List<string?> _units = [];

    public long LastSeq { get; private set; }
    public DateTime? LastSampleAt { get; private set; }
    public long RejectedRequests { get; private set; }
    public TelemetryBoundedRows<TelemetryStoredSample> Samples { get; }
    public TelemetryBoundedRows<TelemetryStoredError> Errors { get; }
    public TelemetryBoundedRows<TelemetryStoredMark> Marks { get; }
    public IReadOnlyList<string?> NameList => _names;
    public IReadOnlyList<string?> UnitList => _units;
    public int NameSlots => _names.Count;

    public IReadOnlyList<TelemetryMetricName> Names => _names
        .Select((name, slot) => name == null ? null : new TelemetryMetricName(slot, name, _units[slot]))
        .OfType<TelemetryMetricName>()
        .ToArray();

    // Rejected: what the start has already dropped, the tags.
    public static TelemetrySessionBuffer Empty(ClientTelemetryStorageOptions options, long rejected = 0)
    {
        return new TelemetrySessionBuffer(options, rejected);
    }

    // A batch over the limits is only counted, but it keeps its slots when they fit: the client counts its values by them. The
    // seq is the cursor of the reading, every batch moves it.
    public void Apply(TelemetryBatch batch, DateTime receivedAt)
    {
        var seq = ++LastSeq;

        if (!TelemetrySessionLimits.FitsNames(batch, _names.Count))
        {
            RejectedRequests++;
            return;
        }

        AddNames(batch);

        if (batch.Rejection != null || !TelemetrySessionLimits.FitsValues(batch, _names.Count))
        {
            RejectedRequests++;
            return;
        }

        Samples.AddRange(batch.Samples.Select(sample => new TelemetryStoredSample(seq, sample.Time, sample.Values, sample.Hidden, receivedAt)));
        Errors.AddRange(batch.Errors.Select(error => new TelemetryStoredError(seq, error.Time, error.Level, error.Message, error.Stack, error.Repeat, receivedAt, TelemetryErrorFingerprint.Of(error.Level, error.Message, error.Stack, error.Category), error.Category)));
        Marks.AddRange(batch.Marks.Select(mark => new TelemetryStoredMark(seq, mark.Time, mark.Name, receivedAt, mark.Detail, mark.DurationMs)));

        if (batch.Samples.Count > 0)
            LastSampleAt = receivedAt;
    }

    private void AddNames(TelemetryBatch batch)
    {
        var known = _names.Count;

        for (var slot = 0; slot < batch.NameSlots; slot++)
        {
            _names.Add(null);
            _units.Add(null);
        }

        // The slot stays either way: the client counts its values by it. A name seen before stays empty here, so the first
        // slot of a name is the only series of the name for a reader.
        foreach (var name in batch.Names)
        {
            if (_names.Contains(name.Name))
                continue;

            _names[known + name.Slot] = name.Name;
            _units[known + name.Slot] = name.Unit;
        }
    }
}
