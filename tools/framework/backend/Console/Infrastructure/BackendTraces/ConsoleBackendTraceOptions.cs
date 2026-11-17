using Microsoft.Extensions.Configuration;

namespace Console.Infrastructure.BackendTraces;

// Where the console reads the backend traces: the HTTP API of the Aspire dashboard (DASHBOARD__API__AUTHMODE=ApiKey, the key of
// DASHBOARD__API__PRIMARYAPIKEY). The dashboard keeps its telemetry in memory: an old trace is gone after its restart.
public sealed record ConsoleBackendTraceOptions
{
    public required Uri BaseUrl { get; init; }
    public required string ApiKey { get; init; }
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(10);

    // A section with Url and ApiKey (the AspireDashboard section of a game); a missing value throws.
    public static ConsoleBackendTraceOptions From(IConfigurationSection section)
    {
        return new ConsoleBackendTraceOptions
        {
            BaseUrl = new Uri(Required(section, "Url")),
            ApiKey = Required(section, "ApiKey")
        };
    }

    private static string Required(IConfigurationSection section, string key)
    {
        var value = section[key];

        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"The backend traces of the console need {section.Path}:{key}");

        return value;
    }
}
