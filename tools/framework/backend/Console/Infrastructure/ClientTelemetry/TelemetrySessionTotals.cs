using Game.ClientTelemetry;

namespace Console.Infrastructure.ClientTelemetry;

// The counters come from the digests: a live session and one lost in a crash of a silo have none.
public sealed record TelemetrySessionTotals(
    int Live,
    int Ended,
    int Lost,
    long Errors,
    long DroppedSamples,
    long DroppedErrors,
    long DroppedMarks,
    long RejectedRequests)
{
    public static TelemetrySessionTotals Of(IReadOnlyCollection<TelemetrySessionInfo> sessions)
    {
        var digests = sessions.Select(session => session.Digest).OfType<TelemetrySessionDigest>().ToArray();

        return new TelemetrySessionTotals(
            sessions.Count(session => session.Status == TelemetrySessionStatus.Live),
            sessions.Count(session => session.Status == TelemetrySessionStatus.Ended),
            sessions.Count(session => session.Status == TelemetrySessionStatus.Lost),
            digests.Sum(digest => digest.ErrorCount),
            digests.Sum(digest => digest.DroppedSamples),
            digests.Sum(digest => digest.DroppedErrors),
            digests.Sum(digest => digest.DroppedMarks),
            digests.Sum(digest => digest.RejectedRequests));
    }
}
