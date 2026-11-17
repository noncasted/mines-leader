using Common.Extensions;
using Internal;
using Infrastructure;
using Infrastructure.Execution;
using Microsoft.Extensions.Logging;

namespace Cluster.Configs;

// First value of a config state, set once by the coordinator: <jsonPath>.json next to the service binaries when the
// file is there, the default value otherwise. A state that already has a value is left as it is.
public static class ConfigsInitializer
{
    public static Task Init<T>(string jsonPath, IAddressableState<T> storage, ILogger logger) where T : class, new()
    {
        return Init(jsonPath, storage, new T(), logger);
    }

    // No catch: a config that failed to initialize stops the coordinator setup instead of running with the default.
    public static async Task Init<T>(string jsonPath, IAddressableState<T> storage, T defaultValue, ILogger logger)
        where T : class, new()
    {
        if (storage.IsInitialized == true)
            return;

        var configPath = Path.Combine(AppContext.BaseDirectory, $"{jsonPath}.json");

        if (File.Exists(configPath) == false)
        {
            await storage.SetValue(defaultValue);
            return;
        }

        logger.LogInformation("[ConfigsInitializer] Seeding config {JsonPath} from {ConfigPath}", jsonPath, configPath);

        var json = await File.ReadAllTextAsync(configPath);
        var value = JsonUtils.Deserialize<T>(json).ThrowIfNull();
        await storage.SetValue(value);
    }
}
