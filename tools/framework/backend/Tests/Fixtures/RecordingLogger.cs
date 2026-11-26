using Microsoft.Extensions.Logging;

namespace Framework.Tests.Fixtures;

// Keeps what a class logged on its own category: a test sees the level, the text and the exception.
public class RecordingLogger<T> : ILogger<T>
{
    private readonly List<(LogLevel Level, string Message, Exception? Exception)> _entries = new();

    public IReadOnlyList<(LogLevel Level, string Message, Exception? Exception)> Entries
    {
        get
        {
            lock (_entries)
                return _entries.ToList();
        }
    }

    public int Count(LogLevel level, string fragment)
    {
        return Entries.Count(entry => entry.Level == level && entry.Message.Contains(fragment, StringComparison.Ordinal));
    }

    public int Count(string fragment)
    {
        return Entries.Count(entry => entry.Message.Contains(fragment, StringComparison.Ordinal));
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        lock (_entries)
            _entries.Add((logLevel, formatter(state, exception), exception));
    }
}
