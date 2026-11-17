namespace Game.ClientTelemetry;

// One kind of error over the closed sessions. Sessions: all of them; SessionIds: the freshest of them, limited.
public sealed record TelemetryGlobalErrorGroup(
    string Fingerprint,
    TelemetryErrorLevel Level,
    string Message,
    long Count,
    int Sessions,
    DateTime FirstSeenAt,
    DateTime LastSeenAt,
    IReadOnlyList<Guid> SessionIds);
