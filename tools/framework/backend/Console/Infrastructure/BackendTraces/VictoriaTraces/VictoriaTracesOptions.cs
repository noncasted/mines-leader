using Microsoft.Extensions.Configuration;

namespace Console.Infrastructure.BackendTraces.VictoriaTraces;

// Where the console reads the backend traces: the query API of VictoriaTraces of the observability stack, inside the network
// `observability` http://victoriatraces:10428 (deploy/observability/CLAUDE.md). No key: the port is not published outside.
public sealed record VictoriaTracesOptions
{
    public required Uri BaseUrl { get; init; }
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(10);

    // A section with Url (the BackendTraces section of a game); a missing value throws.
    public static VictoriaTracesOptions From(IConfigurationSection section)
    {
        return new VictoriaTracesOptions { BaseUrl = new Uri(BackendTraceSettings.Required(section, "Url")) };
    }
}
