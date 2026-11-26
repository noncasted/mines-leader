namespace Game.ClientTelemetry;

// The tags of a session, of its start and of the previous one its client reports: a tag out of form is dropped and counted.
public static class TelemetryTagsParser
{
    public static TelemetrySessionTags Parse(string[]? tags)
    {
        var valid = (tags ?? []).Where(tag => tag != null && TelemetryNames.IsValidName(tag)).Distinct().ToArray();
        var accepted = valid.Take(ClientTelemetryLimits.MaxTagsPerSession).ToArray();

        return new TelemetrySessionTags(accepted, (tags?.Length ?? 0) - accepted.Length);
    }
}
