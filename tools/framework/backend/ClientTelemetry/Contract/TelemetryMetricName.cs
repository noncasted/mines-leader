namespace Game.ClientTelemetry;

// Values of a sample go by the slots. Slot is the position in the list of names of the session:
// a dropped name keeps its slot, otherwise every value after it would land on the wrong metric.
[GenerateSerializer]
public sealed record TelemetryMetricName(int Slot, string Name, string? Unit);
