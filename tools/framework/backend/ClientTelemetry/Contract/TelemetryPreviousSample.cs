namespace Game.ClientTelemetry;

// A sample the client kept of its session that did not close: the main numbers of a frame by their names, a metric without a value
// absent. Time: of that session. Hidden: taken while the page was in the background.
[GenerateSerializer]
public sealed record TelemetryPreviousSample(long Time, bool Hidden, IReadOnlyDictionary<string, double> Values);
