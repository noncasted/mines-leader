namespace Console.Infrastructure.ClientTelemetry;

// The events of one bucket of the scale. Count: with the repeats of the errors. Time: of the first of them.
public sealed record TelemetryTimelineTick(long Time, TelemetryTimelineKind Kind, long Count, string Label);
