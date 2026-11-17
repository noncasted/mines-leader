using Game.ClientTelemetry;

namespace Console.Infrastructure.ClientTelemetry;

public static class TelemetryStartupFacts
{
    public static string State(TelemetryStartupInfo startup)
    {
        return startup.Final ? "final" : "snapshot";
    }
}
