using Microsoft.Extensions.Configuration;

namespace Console.Infrastructure.BackendTraces;

// The values of the configuration section of a source: a missing one throws at the start of the console.
public static class BackendTraceSettings
{
    public static string Required(IConfigurationSection section, string key)
    {
        var value = section[key];

        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"The backend traces of the console need {section.Path}:{key}");

        return value;
    }
}
