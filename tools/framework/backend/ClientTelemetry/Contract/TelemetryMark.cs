namespace Game.ClientTelemetry;

// Detail: scrubbed, null for none. DurationMs: what the mark closes, null for none.
[GenerateSerializer]
public sealed record TelemetryMark(long Time, string Name, string? Detail = null, long? DurationMs = null);
