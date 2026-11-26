namespace Game.ClientTelemetry;

// LastSeq goes into the next "after": the given one when the page is empty.
public sealed record TelemetryReadPage<T>(TelemetrySessionStatus Status, long LastSeq, IReadOnlyList<T> Items);
