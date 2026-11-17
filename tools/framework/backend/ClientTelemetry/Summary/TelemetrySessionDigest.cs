namespace Game.ClientTelemetry;

// The compact result of a closed session, kept in its row with the metadata and searched through the collection. The dropped
// counters: what the limits of the memory of the session took out, so the summary does not cover it. ErrorRows and ErrorCount hold
// no warnings: those are WarningCount (with the repeats); the groups hold both, by level.
[GenerateSerializer]
public sealed record TelemetrySessionDigest(
    long DurationMs,
    int ErrorRows,
    long ErrorCount,
    int Marks,
    long DroppedSamples,
    long DroppedErrors,
    long DroppedMarks,
    long RejectedRequests,
    TelemetrySessionSummary Summary,
    TelemetrySessionErrorGroups ErrorGroups,
    long WarningCount = 0);
