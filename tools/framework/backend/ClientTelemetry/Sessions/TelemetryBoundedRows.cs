namespace Game.ClientTelemetry;

// The rows of a kind in the memory of a session: over the capacity the oldest ones go and are counted.
public sealed class TelemetryBoundedRows<T>
{
    public TelemetryBoundedRows(int capacity)
    {
        _capacity = capacity;
    }

    private readonly int _capacity;
    private readonly List<T> _rows = [];

    public IReadOnlyList<T> Items => _rows;
    public long Dropped { get; private set; }

    public void AddRange(IEnumerable<T> rows)
    {
        _rows.AddRange(rows);

        var over = _rows.Count - _capacity;

        if (over <= 0)
            return;

        _rows.RemoveRange(0, over);
        Dropped += over;
    }
}
