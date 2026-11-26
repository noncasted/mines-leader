using System.Globalization;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Logging;

internal sealed class FileLogger : ILogger
{
    public FileLogger(string categoryName, FileLoggerProvider provider)
    {
        _categoryName = categoryName;
        _provider = provider;
    }

    private readonly string _categoryName;
    private readonly FileLoggerProvider _provider;

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => _provider.IsEnabled(logLevel);

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (IsEnabled(logLevel) == false)
            return;

        var message = formatter(state, exception);
        _provider.WriteEntry(_categoryName, logLevel, message, exception);
    }
}
