namespace Game.ClientTelemetry;

// Message and Stack are already scrubbed: TelemetryBatchParser is the only source of this type. Category: the type of the code that
// logged the error through the logger of the framework (its template is the message), null for the others.
[GenerateSerializer]
public sealed record TelemetryError(long Time, TelemetryErrorLevel Level, string Message, string Stack, int Repeat, string? Category);
