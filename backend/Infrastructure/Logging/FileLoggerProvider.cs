using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Logging;

// Dev-only: registered by ServiceDefaultsExtensions.AddFileLogging in the Development environment.
public class FileLoggerOptions
{
    public const string SectionName = "FileLogging";

    public bool Enabled { get; set; } = true;

    // Directory for {service}.log. Empty: backend/.telemetry/logs of the repository the service runs from.
    public string? Directory { get; set; }

    // Applied on top of the Logging configuration filters.
    public LogLevel MinLevel { get; set; } = LogLevel.Trace;
}

// Log calls only enqueue a line; one background writer appends to the file and flushes once the queue is drained,
// so concurrent loggers never wait on the file or on each other.
public sealed class FileLoggerProvider : ILoggerProvider
{
    public FileLoggerProvider(string serviceName, FileLoggerOptions options)
    {
        var directory = string.IsNullOrWhiteSpace(options.Directory)
            ? TelemetryPaths.GetTelemetryDir("logs") ?? throw new InvalidOperationException(
                $"File logging: no repository root (.git) above {AppContext.BaseDirectory}; " +
                $"set {FileLoggerOptions.SectionName}:Directory or {FileLoggerOptions.SectionName}:Enabled=false")
            : options.Directory;

        System.IO.Directory.CreateDirectory(directory);

        FilePath = Path.Combine(directory, $"{serviceName}.log");
        _minLevel = options.MinLevel;

        var stream = new FileStream(FilePath, FileMode.Append, FileAccess.Write, FileShare.Read);
        _writer = new StreamWriter(stream, new UTF8Encoding(false));

        _writeTask = Task.Run(WriteLoop);

        // Lines still queued when the process ends (crash included) are what explains the end: flush them.
        AppDomain.CurrentDomain.ProcessExit += OnProcessEnd;
        AppDomain.CurrentDomain.UnhandledException += OnProcessEnd;
    }

    private readonly LogLevel _minLevel;
    private readonly StreamWriter _writer;
    private readonly Channel<string> _queue = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
    {
        SingleReader = true
    });
    private readonly Task _writeTask;
    private readonly ConcurrentDictionary<string, FileLogger> _loggers = new();

    private int _disposed;

    public string FilePath { get; }

    public ILogger CreateLogger(string categoryName)
    {
        return _loggers.GetOrAdd(categoryName, name => new FileLogger(name, this));
    }

    // Flushes everything logged before the call; rethrows a failure of the writer.
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
            return;

        AppDomain.CurrentDomain.ProcessExit -= OnProcessEnd;
        AppDomain.CurrentDomain.UnhandledException -= OnProcessEnd;

        _queue.Writer.TryComplete();

        try
        {
            _writeTask.GetAwaiter().GetResult();
        }
        finally
        {
            _writer.Dispose();
        }
    }

    private void OnProcessEnd(object? sender, EventArgs args)
    {
        Dispose();
    }

    internal bool IsEnabled(LogLevel logLevel)
    {
        return logLevel != LogLevel.None && logLevel >= _minLevel;
    }

    internal void WriteEntry(string categoryName, LogLevel logLevel, string message, Exception? exception)
    {
        if (_writeTask.IsFaulted == true)
            throw new InvalidOperationException($"File logger for {FilePath} failed", _writeTask.Exception);

        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);

        var level = logLevel switch
        {
            LogLevel.Trace => "TRC",
            LogLevel.Debug => "DBG",
            LogLevel.Information => "INF",
            LogLevel.Warning => "WRN",
            LogLevel.Error => "ERR",
            LogLevel.Critical => "CRT",
            _ => throw new ArgumentOutOfRangeException(nameof(logLevel), logLevel, null)
        };

        var line = exception == null
            ? $"[{timestamp}] [{level}] [{categoryName}] {message}"
            : $"[{timestamp}] [{level}] [{categoryName}] {message}{Environment.NewLine}{exception}";

        // False only after Dispose: late log records of a stopping host are dropped by design.
        _queue.Writer.TryWrite(line);
    }

    private async Task WriteLoop()
    {
        var reader = _queue.Reader;

        while (await reader.WaitToReadAsync())
        {
            while (reader.TryRead(out var line))
                await _writer.WriteLineAsync(line);

            await _writer.FlushAsync();
        }
    }
}

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
