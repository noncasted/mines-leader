using Common.Extensions;
using Internal;
using Infrastructure;
using Infrastructure.Execution;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Cluster.Configs;

// First value of a config state, set once by the coordinator from <jsonPath>.json in the content root of the host:
// the folder of the service in a container, the folder of the host in a monolith (several hosts share one
// AppContext.BaseDirectory there). A state that already has a value is left as it is.
public static class ConfigsInitializer
{
    // No catch, no default: a missing file is a setup error (the file is not where the host reads it), and a config
    // that failed to initialize stops the coordinator setup instead of running with a guessed value.
    public static async Task Init<T>(
        IHostEnvironment environment,
        string jsonPath,
        IAddressableState<T> storage,
        ILogger logger)
        where T : class, new()
    {
        if (storage.IsInitialized == true)
            return;

        var configPath = Path.Combine(environment.ContentRootPath, $"{jsonPath}.json");

        if (File.Exists(configPath) == false)
            throw new FileNotFoundException($"Config {jsonPath} is missing in the content root of the host", configPath);

        logger.LogInformation("[ConfigsInitializer] Seeding config {JsonPath} from {ConfigPath}", jsonPath, configPath);

        var json = await File.ReadAllTextAsync(configPath);
        var value = JsonUtils.Deserialize<T>(json).ThrowIfNull();
        await storage.SetValue(value);
    }
}
