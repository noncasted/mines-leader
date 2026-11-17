namespace Game.ClientTelemetry;

// The previous session after TelemetryPreviousParser. Rejected: the parts of it that were dropped.
public sealed record TelemetryPreviousParsing(TelemetryPreviousSession? Session, int Rejected);
