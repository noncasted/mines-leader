namespace Game.ClientTelemetry;

// The tags of a session after TelemetryTagsParser. Rejected: how many of them were dropped.
[GenerateSerializer]
public sealed record TelemetrySessionTags(IReadOnlyList<string> Accepted, int Rejected);
