using Microsoft.Extensions.Configuration;

namespace Console.Infrastructure.BackendTraces.AspireDashboard;

// Where the console reads the backend traces: the HTTP API of the Aspire dashboard (DASHBOARD__API__AUTHMODE=ApiKey, the key of
// DASHBOARD__API__PRIMARYAPIKEY). The dashboard keeps its telemetry in memory: an old trace is gone after its restart.
public sealed record AspireDashboardTraceOptions
{
    public required Uri BaseUrl { get; init; }
    public required string ApiKey { get; init; }
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(10);

    // A section with Url and ApiKey (the AspireDashboard section of a game); a missing value throws.
    public static AspireDashboardTraceOptions From(IConfigurationSection section)
    {
        return new AspireDashboardTraceOptions
        {
            BaseUrl = new Uri(BackendTraceSettings.Required(section, "Url")),
            ApiKey = BackendTraceSettings.Required(section, "ApiKey")
        };
    }
}
