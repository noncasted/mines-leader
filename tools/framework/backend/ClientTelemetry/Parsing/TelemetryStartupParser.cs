using Shared;

namespace Game.ClientTelemetry;

// The only way from a startup trace of a client to the model. Names come from the code of a game, but a name may hold a URL (the spans
// of the requests): no list of allowed symbols can fit "Scope: Registry" and a free text, so the name goes through Scrubber.
public static class TelemetryStartupParser
{
    private const int _spanBytes = 42;
    private const int _noParent = -1;
    private const int _timeDecimals = 3;

    public static TelemetryStartupTrace Parse(SharedClientTelemetry.StartupTrace trace)
    {
        var incoming = trace.Spans ?? [];
        var spans = new List<TelemetryStartupSpan>();
        var indices = new int[incoming.Length];
        var bytes = 0;
        var rejected = 0;
        var cutNames = 0;
        var dropped = 0;

        for (var index = 0; index < incoming.Length; index++)
        {
            indices[index] = _noParent;

            var span = incoming[index];
            var name = span == null ? null : CleanName(span.Name);

            if (span == null || name == null || !HasValidValues(span, index) || (span.ParentIndex != _noParent && indices[span.ParentIndex] == _noParent))
            {
                rejected++;
                continue;
            }

            bytes += _spanBytes + name.Length + Math.Min(span.File?.Length ?? 0, ClientTelemetryLimits.MaxStartupFileLength);

            if (spans.Count == ClientTelemetryLimits.MaxStartupSpans || bytes > ClientTelemetryLimits.MaxStartupBytes)
            {
                dropped = incoming.Length - index;
                break;
            }

            if (span.Name.Length > ClientTelemetryLimits.MaxStartupNameLength)
                cutNames++;

            indices[index] = spans.Count;
            spans.Add(new TelemetryStartupSpan(
                span.ParentIndex == _noParent ? _noParent : indices[span.ParentIndex],
                name,
                Math.Round(span.StartMs, _timeDecimals),
                Math.Round(span.DurationMs, _timeDecimals),
                span.Frames,
                span.Unfinished,
                span.AllocBytes,
                span.File != null && TelemetryNames.IsValidFile(span.File) ? span.File : null,
                span.TransferBytes,
                span.Cached));
        }

        var hasDuration = IsTime(trace.DurationMs);

        return new TelemetryStartupTrace
        {
            Device = TelemetryDeviceParser.Parse(trace.Device),
            Name = CleanName(trace.Name) ?? string.Empty,
            Final = trace.Final,
            DurationMs = hasDuration ? Math.Round(trace.DurationMs, _timeDecimals) : 0,
            Frames = Math.Max(trace.Frames, 0),
            Spans = spans,
            DroppedSpans = dropped,
            RejectedSpans = rejected + (hasDuration ? 0 : 1) + (trace.Frames < 0 ? 1 : 0),
            CutNames = cutNames
        };
    }

    // Null: not a name. Cut before the scrubbing too: its cost must not depend on what a client sends, and what stays of a URL or an
    // address is scrubbed all the same. Cut after it: a replacement may be longer than what it replaces.
    private static string? CleanName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Any(char.IsControl))
            return null;

        return Cut(Scrubber.Scrub(Cut(name)));
    }

    private static string Cut(string name)
    {
        return name.Length <= ClientTelemetryLimits.MaxStartupNameLength ? name : name[..ClientTelemetryLimits.MaxStartupNameLength];
    }

    private static bool HasValidValues(SharedClientTelemetry.StartupSpan span, int index)
    {
        return IsTime(span.StartMs)
               && IsTime(span.DurationMs)
               && span.Frames >= 0
               && span.AllocBytes >= 0
               && span.TransferBytes >= 0
               && span.ParentIndex >= _noParent
               && span.ParentIndex < index;
    }

    private static bool IsTime(double value)
    {
        return double.IsFinite(value) && value is >= 0 and <= ClientTelemetryLimits.MaxStartupMs;
    }
}
