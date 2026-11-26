namespace Game.ClientTelemetry;

// The session of the same browser before this one that did not close on the client (the page was killed, reloaded or frozen inside
// it), as the client kept it: its last sample and marks may be later than what the server got. Null times: unknown.
[GenerateSerializer]
public sealed record TelemetryPreviousSession(
    Guid SessionId,
    IReadOnlyList<string> Tags,
    long? LastSampleTime,
    long? SinceMs,
    IReadOnlyList<TelemetryMark> Marks)
{
    // The last samples of the client, oldest first. A row written before them reads as an empty list. The ids of the parameters
    // above are their positions.
    [Id(5)] public IReadOnlyList<TelemetryPreviousSample> Samples { get; init; } = [];
}
