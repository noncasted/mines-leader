using Shared;

namespace Game.ClientTelemetry;

// A mark of a batch or of the previous session, its form already checked by the caller.
internal static class TelemetryMarkParser
{
    // A detail that is not a text is dropped alone. Cut before the scrubbing, so its cost does not depend on the client, and after it.
    public static TelemetryMark Read(SharedClientTelemetry.Mark mark)
    {
        var detail = string.IsNullOrWhiteSpace(mark.Detail) || mark.Detail.Any(char.IsControl)
            ? null
            : CutDetail(Scrubber.Scrub(CutDetail(mark.Detail)));

        return new TelemetryMark(mark.Time, mark.Name, detail, mark.DurationMs);
    }

    private static string CutDetail(string detail)
    {
        return detail.Length <= ClientTelemetryLimits.MaxMarkDetailLength ? detail : detail[..ClientTelemetryLimits.MaxMarkDetailLength];
    }
}
