namespace Game.ClientTelemetry;

// The kinds of errors over the digests of the sessions, from the most frequent, of equal counts from the freshest. What it does not
// cover is counted: SessionsWithoutDigest (live ones and ones lost in a crash), OmittedGroups and OmittedCount (over the limit of a
// digest), DroppedErrors (rows out of the memory of a session).
public sealed record TelemetryGlobalErrorGroups(
    int TotalGroups,
    int CountedSessions,
    int SessionsWithoutDigest,
    int OmittedGroups,
    long OmittedCount,
    long DroppedErrors,
    IReadOnlyList<TelemetryGlobalErrorGroup> Groups)
{
    public const int MaxSessionIds = 20;

    public static TelemetryGlobalErrorGroups Of(IEnumerable<TelemetrySessionInfo> sessions, int limit)
    {
        var all = sessions.ToArray();
        var counted = all.Where(session => session.Digest != null).ToArray();

        var groups = counted
            .SelectMany(session => session.Digest!.ErrorGroups.Groups.Select(group => (session.Id, Group: group)))
            .GroupBy(item => item.Group.Fingerprint)
            .Select(Group)
            .OrderByDescending(group => group.Count)
            .ThenByDescending(group => group.LastSeenAt)
            .ThenBy(group => group.Fingerprint, StringComparer.Ordinal)
            .ToArray();

        return new TelemetryGlobalErrorGroups(
            groups.Length,
            counted.Length,
            all.Length - counted.Length,
            counted.Sum(session => session.Digest!.ErrorGroups.OmittedGroups),
            counted.Sum(session => session.Digest!.ErrorGroups.OmittedCount),
            counted.Sum(session => session.Digest!.DroppedErrors),
            groups.Take(limit).ToArray());
    }

    private static TelemetryGlobalErrorGroup Group(IEnumerable<(Guid Id, TelemetryErrorGroup Group)> items)
    {
        var freshest = items.OrderByDescending(item => item.Group.LastSeenAt).ThenBy(item => item.Id).ToArray();
        var newest = freshest[0].Group;

        return new TelemetryGlobalErrorGroup(
            newest.Fingerprint,
            newest.Level,
            newest.Message,
            freshest.Sum(item => item.Group.Count),
            freshest.Length,
            freshest.Min(item => item.Group.FirstSeenAt),
            newest.LastSeenAt,
            freshest.Take(MaxSessionIds).Select(item => item.Id).ToArray(),
            newest.Category);
    }
}
