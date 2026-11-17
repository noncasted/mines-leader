using System.Globalization;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Logging;

// Dev-only: registered by ServiceDefaultsExtensions.AddFileLogging in the Development environment.
// The properties are init, not private set: ConfigurationBinder writes init and silently skips a private setter.
public class FileLoggerOptions
{
    public const string SectionName = "FileLogging";

    public bool Enabled { get; init; } = true;

    // Directory for {service}.log. Empty: backend/.telemetry/logs of the repository the service runs from.
    public string? Directory { get; init; }

    // Applied on top of the Logging configuration filters.
    public LogLevel MinLevel { get; init; } = LogLevel.Trace;
}
