namespace Game.Session;

public class ObservationEventBuffer : IObservationEventBuffer
{
    private const int MaxLines = 2000;

    private readonly object _lock = new();
    private readonly List<(int Cursor, string Line)> _entries = new();
    private int _nextCursor;

    public int Cursor
    {
        get
        {
            lock (_lock)
                return _nextCursor - 1;
        }
    }

    public void Append(string line)
    {
        lock (_lock)
        {
            _entries.Add((_nextCursor, line));
            _nextCursor++;

            while (_entries.Count > MaxLines)
                _entries.RemoveAt(0);
        }
    }

    public IReadOnlyList<string> TakeAfter(int exclusiveCursor)
    {
        lock (_lock)
        {
            var result = new List<string>();

            foreach (var (cursor, line) in _entries)
            {
                if (cursor > exclusiveCursor)
                    result.Add(line);
            }

            return result;
        }
    }
}
