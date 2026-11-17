namespace Game.ClientTelemetry;

// Message and Stack are already scrubbed: TelemetryBatchParser is the only source of this type.
[GenerateSerializer]
public sealed record TelemetryError(long Time, TelemetryErrorLevel Level, string Message, string Stack, int Repeat);
