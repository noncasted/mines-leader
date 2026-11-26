using System.Globalization;

namespace Console.Infrastructure.ClientTelemetry;

public static class TelemetryValueFormat
{
    public const string Unknown = "-";

    private const string _bytesUnit = "bytes";
    private const string _millisecondsUnit = "ms";
    private const double _kilo = 1024;
    private const double _millisecondsInSecond = 1000;

    private static readonly string[] _byteSizes = ["B", "KB", "MB", "GB", "TB"];

    public static string Value(double value, string? unit)
    {
        return unit switch
        {
            _bytesUnit => Bytes(value),
            _millisecondsUnit => $"{Number(value)} ms",
            _ => Number(value)
        };
    }

    public static string Optional(double? value, string? unit)
    {
        return value is { } known ? Value(known, unit) : Unknown;
    }

    // A time of a session, from its start.
    public static string Time(long milliseconds)
    {
        var time = TimeSpan.FromMilliseconds(milliseconds);

        return time.TotalHours >= 1
            ? time.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture)
            : time.ToString(@"m\:ss", CultureInfo.InvariantCulture);
    }

    public static string Duration(TimeSpan duration)
    {
        return Time((long)duration.TotalMilliseconds);
    }

    // A time of a startup trace: a load is told in milliseconds, a wait in seconds.
    public static string Milliseconds(double milliseconds)
    {
        return milliseconds >= _millisecondsInSecond
            ? $"{Number(Math.Round(milliseconds / _millisecondsInSecond, 2))} s"
            : $"{Number(Math.Round(milliseconds, 1))} ms";
    }

    public static string Timestamp(DateTime time)
    {
        // The times of the telemetry are UTC times of the server.
        return $"{time.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)} UTC";
    }

    private static string Bytes(double value)
    {
        var size = 0;
        var scaled = Math.Abs(value);

        while (scaled >= _kilo && size < _byteSizes.Length - 1)
        {
            scaled /= _kilo;
            size++;
        }

        return $"{Number(Math.Sign(value) * scaled)} {_byteSizes[size]}";
    }

    private static string Number(double value)
    {
        return value.ToString("#,0.##", CultureInfo.InvariantCulture);
    }
}
