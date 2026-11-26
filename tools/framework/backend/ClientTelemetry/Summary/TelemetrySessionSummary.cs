namespace Game.ClientTelemetry;

// Samples: the ones counted; HiddenSamples: left out, the page was in the background. A metric without a point is not listed.
[GenerateSerializer]
public sealed record TelemetrySessionSummary(int Samples, int HiddenSamples, IReadOnlyList<TelemetryMetricSummary> Metrics);
