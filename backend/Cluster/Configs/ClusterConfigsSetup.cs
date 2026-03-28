using Common.Extensions;
using Common.Reactive;
using Infrastructure;
using Infrastructure.Execution;

namespace Cluster.Configs;

public class ClusterConfigsSetup : ICoordinatorSetupCompleted
{
    public ClusterConfigsSetup(
        ICardConfigs cards,
        IBotConfig bots,
        IGameModeConfig gameMode,
        IRatingConfig rating,
        ISideEffectsConfig sideEffects,
        IMessageQueueConfig messageQueue,
        ITaskBalancerConfig taskBalancer)
    {
        _cards = cards;
        _bots = bots;
        _gameMode = gameMode;
        _rating = rating;
        _sideEffects = sideEffects;
        _messageQueue = messageQueue;
        _taskBalancer = taskBalancer;
    }

    private readonly ICardConfigs _cards;
    private readonly IBotConfig _bots;
    private readonly IGameModeConfig _gameMode;
    private readonly IRatingConfig _rating;
    private readonly ISideEffectsConfig _sideEffects;
    private readonly IMessageQueueConfig _messageQueue;
    private readonly ITaskBalancerConfig _taskBalancer;

    public async Task OnCoordinatorSetupCompleted(IReadOnlyLifetime lifetime)
    {
        await InitializeConfig("config.cards", _cards);
        await InitializeConfig("config.bot", _bots);
        await InitializeConfig("config.gameMode", _gameMode);
        await InitializeConfig("config.rating", _rating);
        await InitializeConfig("config.sideEffects", _sideEffects);
        await InitializeConfig("config.messageQueue", _messageQueue);
        await InitializeConfig("config.taskBalancer", _taskBalancer);

        return;

        async Task InitializeConfig<T>(string jsonPath, IAddressableState<T> storage) where T : class, new()
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