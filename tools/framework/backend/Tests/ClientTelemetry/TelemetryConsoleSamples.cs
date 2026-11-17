using Game.ClientTelemetry;

namespace Framework.Tests.ClientTelemetry;

// Rows and sessions for the tests of the console pages: only what a test looks at differs.
public static class TelemetryConsoleSamples
{
    private static readonly DateTime _start = new(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc);

    public static TelemetryStoredSample Sample(long seq, long time, double? value, bool hidden = false)
    {
        return new TelemetryStoredSample(seq, time, [value], hidden, _start);
    }

    public static TelemetryStoredError Error(long seq, long time, string fingerprint, string message = "failure", int repeat = 1)
    {
        return new TelemetryStoredError(seq, time, TelemetryErrorLevel.Error, message, "at Stack ()", repeat, _start, fingerprint);
    }

    public static TelemetryStoredMark Mark(long seq, long time, string name)
    {
        return new TelemetryStoredMark(seq, time, name, _start);
    }

    public static TelemetrySessionInfo SessionOf(
        TelemetrySessionStatus status = TelemetrySessionStatus.Ended,
        string? platform = "web",
        TelemetrySessionDigest? digest = null,
        DateTime? lastSampleAt = null,
        IReadOnlyList<string>? tags = null,
        Guid? id = null)
    {
        return new TelemetrySessionInfo
        {
            Id = id ?? Guid.NewGuid(),
            Status = status,
            Device = new TelemetryDevice { Platform = platform },
            StartedAt = _start,
            LastSampleAt = lastSampleAt,
            LastSeq = 1,
            Names = [new TelemetryMetricName(0, "frame.fps", "count")],
            NameSlots = 1,
            Digest = digest,
            Tags = tags ?? []
        };
    }

    public static TelemetrySessionDigest Digest(
        long durationMs = 1000,
        long errorCount = 0,
        long droppedSamples = 0,
        long droppedErrors = 0,
        long droppedMarks = 0,
        long rejectedRequests = 0,
        params TelemetryMetricSummary[] metrics)
    {
        return new TelemetrySessionDigest(
            durationMs,
            0,
            errorCount,
            0,
            droppedSamples,
            droppedErrors,
            droppedMarks,
            rejectedRequests,
            new TelemetrySessionSummary(1, 0, metrics),
            new TelemetrySessionErrorGroups([], 0, 0));
    }

    public static TelemetryMetricSummary Metric(string name, double average)
    {
        return new TelemetryMetricSummary(name, "count", 1, average, average, average, average, average, null);
    }

    public static TelemetryGlobalErrorGroup Group(string fingerprint, long count, DateTime lastSeenAt, params Guid[] sessions)
    {
        return new TelemetryGlobalErrorGroup(fingerprint, TelemetryErrorLevel.Error, "failure", count, sessions.Length, lastSeenAt, lastSeenAt, sessions);
    }
}
