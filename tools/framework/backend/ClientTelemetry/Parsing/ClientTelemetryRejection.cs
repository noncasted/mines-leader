namespace Game.ClientTelemetry;

[GenerateSerializer]
public enum ClientTelemetryRejection
{
    BatchTooLarge,
    Malformed,
    TooManyNames,
    TooManySamples,
    TooManyValues,
    TooManyErrors,
    TooManyMarks,
    MessageTooLong,
    StackTooLong
}
