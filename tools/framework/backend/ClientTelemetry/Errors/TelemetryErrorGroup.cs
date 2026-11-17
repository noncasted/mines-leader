namespace Game.ClientTelemetry;

// One kind of error in a session. Count: with the repeats; Rows: the stored rows, without them. The times are of the server.
[GenerateSerializer]
public sealed record TelemetryErrorGroup(
    string Fingerprint,
    TelemetryErrorLevel Level,
    string Message,
    long Count,
    int Rows,
    DateTime FirstSeenAt,
    DateTime LastSeenAt);
