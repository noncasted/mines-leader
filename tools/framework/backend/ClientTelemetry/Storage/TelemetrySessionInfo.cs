namespace Game.ClientTelemetry;

[GenerateSerializer]
public sealed record TelemetrySessionInfo
{
    [Id(0)] public required Guid Id { get; init; }
    [Id(1)] public required TelemetrySessionStatus Status { get; init; }
    [Id(2)] public required TelemetryDevice Device { get; init; }
    [Id(3)] public required DateTime StartedAt { get; init; }
    [Id(4)] public required DateTime? LastSampleAt { get; init; }

    // The cursor of a live reading: "after" of the next page. Its name in the answers stays until the skill reads it by a new one.
    [Id(5)] [System.Text.Json.Serialization.JsonPropertyName("ackSeq")] public required long LastSeq { get; init; }

    // Names with a value: Slot is the index in TelemetryStoredSample.Values. NameSlots counts the empty slots too (a name the
    // parser dropped, a repeated name): values of those slots belong to nothing.
    [Id(6)] public required IReadOnlyList<TelemetryMetricName> Names { get; init; }
    [Id(7)] public required int NameSlots { get; init; }

    [Id(8)] public DateTime? ClosedAt { get; init; }

    // Of a closed session; null while it is live and when it was lost in a crash, with its log.
    [Id(9)] public TelemetrySessionDigest? Digest { get; init; }

    [Id(10)] public IReadOnlyList<string> Tags { get; init; } = [];

    // The record of the startup trace of the client: null when there is none, also in a row written before the traces.
    [Id(11)] public Guid? StartupId { get; init; }

    // The session before this one that did not close on its client, as the client reported it: null when there is none.
    [Id(12)] public TelemetryPreviousSession? Previous { get; init; }
}
