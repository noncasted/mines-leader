using Common.Extensions;
using Common.Reactive;
using Infrastructure;

namespace Cluster.Configs;

public class ClusterConfigsSetup : ICoordinatorSetupCompleted
{
    public ClusterConfigsSetup(
        ICardConfigs cards,
        IBotConfig bots,
        IGameModeConfig gameMode)
    {
        _cards = cards;
        _bots = bots;
        _gameMode = gameMode;
    }

    private readonly ICardConfigs _cards;
    private readonly IBotConfig _bots;
    private readonly IGameModeConfig _gameMode;

    public async Task OnCoordinatorSetupCompleted(IReadOnlyLifetime lifetime)
    {
        await InitializeConfig("config.cards", _cards);
        await InitializeConfig("config.bot", _bots);
        await InitializeConfig("config.gameMode", _gameMode);

        return;

        async Task InitializeConfig<T>(string jsonPath, IRawAddressableState<T> storage) where T : class, new()
        {
            try
            {
                if (storage.IsInitialized == true)
                    return;
                
                var configPath = Path.Combine(AppContext.BaseDirectory, $"{jsonPath}.json");

                if (File.Exists(configPath) == false)
                {
                    await storage.SetValue(new T());
                    return;
                }

                var json = await File.ReadAllTextAsync(configPath);
                var value = JsonUtils.Deserialize<T>(json)!;
                await storage.SetValue(value);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to load bot config: {e.Message}");
            }
        }
    }
}