using Game.ClientTelemetry;

namespace Console.Infrastructure.ClientTelemetry;

// What the list of sessions shows: a null filter takes everything.
public sealed record TelemetrySessionQuery(TelemetrySessionStatus? Status, string? Platform, string? Tag, int Page);
