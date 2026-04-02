using Common.Extensions;
using Common.Reactive;
using Infrastructure;
using Infrastructure.Execution;
using Microsoft.Extensions.Logging;

namespace Cluster.Configs;

public class ClusterConfigsSetup : ICoordinatorSetupCompleted
{
    public ClusterConfigsSetup(
        ICardConfigs cards,
        IBotConfig bots,
        IGameModeConfig gameMode,
        IRatingConfig rating,
        ISideEffectsConfig sideEffects,
        IDurableQueueConfig messageQueue,
        ITaskBalancerConfig taskBalancer,
        IRuntimePipeConfig runtimePipe,
        IRuntimeChannelConfig runtimeChannel,
        ITransactionConfig transactionConfig,
        ILogger<ClusterConfigsSetup> logger)
    {
        _cards = cards;
        _bots = bots;
        _gameMode = gameMode;
        _rating = rating;
        _sideEffects = sideEffects;
        _durableQueue = messageQueue;
        _taskBalancer = taskBalancer;
        _runtimePipe = runtimePipe;
        _runtimeChannel = runtimeChannel;
        _transactionConfig = transactionConfig;
        _logger = logger;
    }

    private readonly ICardConfigs _cards;
    private readonly IBotConfig _bots;
    private readonly IGameModeConfig _gameMode;
    private readonly IRatingConfig _rating;
    private readonly ISideEffectsConfig _sideEffects;
    private readonly IDurableQueueConfig _durableQueue;
    private readonly ITaskBalancerConfig _taskBalancer;
    private readonly IRuntimePipeConfig _runtimePipe;
    private readonly IRuntimeChannelConfig _runtimeChannel;
    private readonly ITransactionConfig _transactionConfig;
    private readonly ILogger<ClusterConfigsSetup> _logger;

    public async Task OnCoordinatorSetupCompleted(IReadOnlyLifetime lifetime)
    {
        await InitializeConfig("config.cards", _cards);
        await InitializeConfig("config.bot", _bots);
        await InitializeConfig("config.gameMode", _gameMode);
        await InitializeConfig("config.rating", _rating);
        await InitializeConfig("config.sideEffects", _sideEffects);
        await InitializeConfig("config.durableQueue", _durableQueue);
        await InitializeConfig("config.taskBalancer", _taskBalancer);
        await InitializeConfig("config.runtimePipe", _runtimePipe);
        await InitializeConfig("config.runtimeChannel", _runtimeChannel);
        await InitializeConfig("config.transaction", _transactionConfig);

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
                _logger.LogError(e, "[ClusterConfigsSetup] Failed to load config {JsonPath}", jsonPath);
            }
        }
    }
}