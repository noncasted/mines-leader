using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Framework.Tests.ClientTelemetry;

// The lines of the grain of a session.
public sealed class CapturingLogs : ILoggerProvider, ILogger
{
    private readonly ConcurrentQueue<string> _lines = new();

    public IEnumerable<string> Lines => _lines;

    public ILogger CreateLogger(string categoryName) => categoryName.Contains("ClientTelemetry") ? this : Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        _lines.Enqueue(formatter(state, exception));
    }

    public void Dispose()
    {
    }
}
