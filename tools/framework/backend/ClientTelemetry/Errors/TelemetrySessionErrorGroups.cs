namespace Game.ClientTelemetry;

// The kinds of errors of a closed session, from the most frequent. They lie in the row of its metadata, which every host keeps in
// memory for the whole retention, so both the number and the text are limited; the full text and the stack stay in the log, and
// what went over is only counted. Covers the rows the memory of the session has kept (DroppedErrors of the digest).
[GenerateSerializer]
public sealed record TelemetrySessionErrorGroups(IReadOnlyList<TelemetryErrorGroup> Groups, int OmittedGroups, long OmittedCount)
{
    public const int MaxGroups = 12;
    public const int MaxMessageLength = 120;

    public static TelemetrySessionErrorGroups Of(IReadOnlyList<TelemetryStoredError> rows)
    {
        var groups = rows
            .GroupBy(row => row.Fingerprint)
            .Select(Group)
            .OrderByDescending(group => group.Count)
            .ThenByDescending(group => group.LastSeenAt)
            .ThenBy(group => group.Fingerprint, StringComparer.Ordinal)
            .ToArray();

        var omitted = groups.Skip(MaxGroups).ToArray();

        return new TelemetrySessionErrorGroups(groups.Take(MaxGroups).ToArray(), omitted.Length, omitted.Sum(group => group.Count));
    }

    private static TelemetryErrorGroup Group(IGrouping<string, TelemetryStoredError> rows)
    {
        var last = rows.Last();

        return new TelemetryErrorGroup(
            rows.Key,
            last.Level,
            last.Message[..Math.Min(last.Message.Length, MaxMessageLength)],
            rows.Sum(row => (long)row.Repeat),
            rows.Count(),
            rows.First().ReceivedAt,
            last.ReceivedAt);
    }
}
