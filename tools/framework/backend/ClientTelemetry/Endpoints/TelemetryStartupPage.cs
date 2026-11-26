namespace Game.ClientTelemetry;

// GET .../startups/{id}: the record, its spans and the sessions that refer to it.
public sealed record TelemetryStartupPage(TelemetryStartupInfo Startup, IReadOnlyList<TelemetryStartupSpanFacts> Spans, IReadOnlyList<TelemetrySessionInfo> Sessions);
