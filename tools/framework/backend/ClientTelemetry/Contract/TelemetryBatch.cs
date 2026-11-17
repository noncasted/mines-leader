namespace Game.ClientTelemetry;

// A batch after TelemetryBatchParser: the only source of this type, whatever a client sent. A rejected batch holds no rows, only
// the names: the client counts its values by the slots, so the slots must not get lost with the rows.
[GenerateSerializer]
public sealed record TelemetryBatch
{
    // The accepted new names. A dropped name keeps its slot (NameSlots counts them all), see TelemetryMetricName.
    [Id(0)] public IReadOnlyList<TelemetryMetricName> Names { get; init; } = [];
    [Id(1)] public int NameSlots { get; init; }
    [Id(2)] public IReadOnlyList<TelemetrySample> Samples { get; init; } = [];
    [Id(3)] public IReadOnlyList<TelemetryError> Errors { get; init; } = [];
    [Id(4)] public IReadOnlyList<TelemetryMark> Marks { get; init; } = [];
    [Id(5)] public ClientTelemetryRejection? Rejection { get; init; }
}
