using Newtonsoft.Json;

namespace Game.ClientTelemetry;

// A null value: the metric has no value in this sample. Hidden: taken while the page was in the background.
[GenerateSerializer]
[JsonConverter(typeof(TelemetryStoredSampleJsonConverter))]
public sealed record TelemetryStoredSample(long Seq, long Time, IReadOnlyList<double?> Values, bool Hidden, DateTime ReceivedAt);
