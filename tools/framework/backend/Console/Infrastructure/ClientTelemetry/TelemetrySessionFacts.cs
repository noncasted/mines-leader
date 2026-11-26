using Game.ClientTelemetry;

namespace Console.Infrastructure.ClientTelemetry;

public static class TelemetrySessionFacts
{
    public const string LostMeaning = "The socket closed or the safety timer ran out: this does not prove that the client died";

    private const string _fpsMetric = "frame.fps";

    // Without a digest (live, lost in a crash of a silo) the session has lasted up to its last sample: ClosedAt of a lost one is the time of the
    // pass that noticed it.
    public static TimeSpan Duration(TelemetrySessionInfo session)
    {
        return session.Digest is { } digest
            ? TimeSpan.FromMilliseconds(digest.DurationMs)
            : (session.LastSampleAt ?? session.StartedAt) - session.StartedAt;
    }

    public static double? AverageFps(TelemetrySessionInfo session)
    {
        return session.Digest is { } digest ? AverageFps(digest.Summary) : null;
    }

    public static double? AverageFps(TelemetrySessionSummary summary)
    {
        return summary.Metrics.FirstOrDefault(metric => metric.Name == _fpsMetric)?.Average;
    }

    public static string Device(TelemetryDevice device)
    {
        string?[] parts =
        [
            device.Platform,
            Versioned(device.Browser, device.BrowserMajor),
            Versioned(device.Os, device.OsMajor),
            device.Cores is { } cores ? $"{cores} cores" : null,
            device is { ScreenWidth: { } width, ScreenHeight: { } height } ? $"{width}x{height}" : null
        ];

        var known = parts.OfType<string>().ToArray();

        return known.Length == 0 ? TelemetryValueFormat.Unknown : string.Join(" · ", known);
    }

    private static string? Versioned(string? name, int? major)
    {
        return name == null || major == null ? name : $"{name} {major}";
    }
}
