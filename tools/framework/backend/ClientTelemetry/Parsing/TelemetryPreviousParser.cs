using Shared;

namespace Game.ClientTelemetry;

// What a client reports of its session before this one that did not close there.
public static class TelemetryPreviousParser
{
    // A wrong id drops the whole: it is the only thing that ties it to a session. Anything else of a wrong form is dropped alone.
    public static TelemetryPreviousParsing Parse(SharedClientTelemetry.PreviousSession? previous)
    {
        if (previous == null)
            return new TelemetryPreviousParsing(null, 0);

        if (previous.SessionId == Guid.Empty)
            return new TelemetryPreviousParsing(null, 1);

        var tags = TelemetryTagsParser.Parse(previous.Tags);
        var marks = previous.Marks ?? [];
        var valid = marks.Where(mark => mark != null && mark.Time >= 0 && mark.Time <= ClientTelemetryLimits.MaxPreviousMs && mark.DurationMs is null or >= 0
                                        && mark.Name != null && TelemetryNames.IsValidName(mark.Name))
                         .Select(TelemetryMarkParser.Read)
                         .ToArray();
        var kept = valid.Skip(Math.Max(0, valid.Length - ClientTelemetryLimits.MaxPreviousMarks)).ToArray();
        var lastSample = Time(previous.LastSampleTime);
        var since = Time(previous.SinceMs);
        var samples = TelemetryPreviousSamplesParser.Parse(previous.SampleNames, previous.Samples);
        var rejected = tags.Rejected + (marks.Length - kept.Length) + samples.Rejected
                       + (lastSample == null && previous.LastSampleTime != -1 ? 1 : 0) + (since == null && previous.SinceMs != -1 ? 1 : 0);

        return new TelemetryPreviousParsing(new TelemetryPreviousSession(previous.SessionId, tags.Accepted, lastSample, since, kept) { Samples = samples.Accepted }, rejected);
    }

    private static long? Time(long time)
    {
        return time >= 0 && time <= ClientTelemetryLimits.MaxPreviousMs ? time : null;
    }
}
