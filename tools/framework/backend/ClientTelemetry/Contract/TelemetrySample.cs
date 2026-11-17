namespace Game.ClientTelemetry;

// A null value: the metric has no value in this sample. Hidden: taken while the page was in the background.
[GenerateSerializer]
public sealed record TelemetrySample(long Time, IReadOnlyList<double?> Values, bool Hidden);
