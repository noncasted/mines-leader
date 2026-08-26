using Common.Extensions;
using Common.Reactive;
using Infrastructure;
using Infrastructure.Execution;
using Microsoft.Extensions.Logging;
using Shared;

namespace Cluster.Configs;

public class ClusterConfigsSetup : ICoordinatorSetupCompleted
{
    public ClusterConfigsSetup(
        ICardConfigs cards,
        IBotConfig bots,
        IGameModeConfig gameMode,
        IMatchMakingConfig matchMaking,
        IRatingConfig rating,
        ISideEffectsConfig sideEffects,
        IDurableQueueConfig messageQueue,
        ITaskBalancerConfig taskBalancer,
        IRuntimePipeConfig runtimePipe,
        IRuntimeChannelConfig runtimeChannel,
        ITransactionConfig transactionConfig,
        ILootProgressionConfig lootProgression,
        IUserDeckConfig userDeckConfig,
        IPlayerConfig playerConfig,
        ILogger<ClusterConfigsSetup> logger)
    {
        _cards = cards;
        _bots = bots;
        _gameMode = gameMode;
        _matchMaking = matchMaking;
        _rating = rating;
        _sideEffects = sideEffects;
        _durableQueue = messageQueue;
        _taskBalancer = taskBalancer;
        _runtimePipe = runtimePipe;
        _runtimeChannel = runtimeChannel;
        _transactionConfig = transactionConfig;
        _lootProgression = lootProgression;
        _userDeckConfig = userDeckConfig;
        _playerConfig = playerConfig;
        _logger = logger;
    }

    private readonly ICardConfigs _cards;
    private readonly IBotConfig _bots;
    private readonly IGameModeConfig _gameMode;
    private readonly IMatchMakingConfig _matchMaking;
    private readonly IRatingConfig _rating;
    private readonly ISideEffectsConfig _sideEffects;
    private readonly IDurableQueueConfig _durableQueue;
    private readonly ITaskBalancerConfig _taskBalancer;
    private readonly IRuntimePipeConfig _runtimePipe;
    private readonly IRuntimeChannelConfig _runtimeChannel;
    private readonly ITransactionConfig _transactionConfig;
    private readonly ILootProgressionConfig _lootProgression;
    private readonly IUserDeckConfig _userDeckConfig;
    private readonly IPlayerConfig _playerConfig;
    private readonly ILogger<ClusterConfigsSetup> _logger;

    public async Task OnCoordinatorSetupCompleted(IReadOnlyLifetime lifetime)
    {
        await InitConfig("config.cards", _cards);
        await InitConfig("config.bot", _bots);
        await InitConfig("config.gameMode", _gameMode);
        await InitConfigWithDefault("config.matchMaking", _matchMaking, MatchMakingOptions.CreateDefault());
        await InitConfig("config.rating", _rating);
        await InitConfig("config.sideEffects", _sideEffects);
        await InitConfig("config.durableQueue", _durableQueue);
        await InitConfig("config.taskBalancer", _taskBalancer);
        await InitConfig("config.runtimePipe", _runtimePipe);
        await InitConfig("config.runtimeChannel", _runtimeChannel);
        await InitConfig("config.transaction", _transactionConfig);
        await InitConfigWithDefault("config.lootProgression", _lootProgression, LootProgressionOptions.CreateDefault());
        await InitConfig("config.userDeck", _userDeckConfig);
        await InitConfig("config.player", _playerConfig);

        return;

        Task InitConfig<T>(string jsonPath, IAddressableState<T> storage) where T : class, new()
        {
            return InitConfigWithDefault(jsonPath, storage, new T());
        }

        async Task InitConfigWithDefault<T>(string jsonPath, IAddressableState<T> storage, T defaultValue)
            where T : class, new()
        {
            try
            {
                if (storage.IsInitialized == true)
                    return;

                var configPath = Path.Combine(AppContext.BaseDirectory, $"{jsonPath}.json");

                if (File.Exists(configPath) == false)
                {
                    await storage.SetValue(defaultValue);
                    return;
                }

                var json = await File.ReadAllTextAsync(configPath);
                var value = JsonUtils.Deserialize<T>(json).ThrowIfNull();
                await storage.SetValue(value);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "[ClusterConfigsSetup] Failed to load config {JsonPath}", jsonPath);
            }
        }
    }
}