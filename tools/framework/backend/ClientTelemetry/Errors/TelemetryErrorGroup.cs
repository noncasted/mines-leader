namespace Game.ClientTelemetry;

// One kind of error in a session. Count: with the repeats; Rows: the stored rows, without them. The times are of the server.
// Category: of the logger, null for none and for a digest stored before it.
[GenerateSerializer]
public sealed record TelemetryErrorGroup(
    string Fingerprint,
    TelemetryErrorLevel Level,
    string Message,
    long Count,
    int Rows,
    DateTime FirstSeenAt,
    DateTime LastSeenAt,
    string? Category = null);
