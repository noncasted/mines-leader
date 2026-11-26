namespace Game.ClientTelemetry;

[GenerateSerializer]
public sealed record TelemetryStoredError(
    long Seq,
    long Time,
    TelemetryErrorLevel Level,
    string Message,
    string Stack,
    int Repeat,
    DateTime ReceivedAt,
    string Fingerprint,
    string? Category = null);
