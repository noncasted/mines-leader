using Common.Extensions;
using Internal;
using Infrastructure;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared;

namespace Cluster.Configs;

public static class GameConfigsExtensions
{
    // Config states of the game. The infrastructure configs come from the framework (AddConfigs).
    public static IHostApplicationBuilder AddGameConfigs(this IHostApplicationBuilder builder)
    {
        builder.AddAddressableState<CardConfigsState>()
               .As<ICardConfigs>();

        builder.AddAddressableState<BotConfigState>()
               .As<IBotConfig>();

        builder.AddAddressableState<GameModeConfigState>()
               .As<IGameModeConfig>();

        builder.AddAddressableState<MatchMakingConfigState>()
               .As<IMatchMakingConfig>();

        builder.AddAddressableState<RatingConfigState>()
               .As<IRatingConfig>();

        builder.AddAddressableState<InGameAchievementConfigState>()
               .As<IInGameAchievementConfig>();

        builder.AddAddressableState<UserDeckConfigState>()
               .As<IUserDeckConfig>();

        builder.AddAddressableState<PlayerConfigState>()
               .As<IPlayerConfig>();

        return builder;
    }
}

// First values of the game configs, set by the coordinator (see ConfigsInitializer).
public class GameConfigsSetup : ICoordinatorSetupCompleted
{
    public GameConfigsSetup(
        IHostEnvironment environment,
        ICardConfigs cards,
        IBotConfig bots,
        IGameModeConfig gameMode,
        IMatchMakingConfig matchMaking,
        IRatingConfig rating,
        IInGameAchievementConfig achievements,
        IUserDeckConfig userDeck,
        IPlayerConfig player,
        ILogger<GameConfigsSetup> logger)
    {
        _environment = environment;
        _cards = cards;
        _bots = bots;
        _gameMode = gameMode;
        _matchMaking = matchMaking;
        _rating = rating;
        _achievements = achievements;
        _userDeck = userDeck;
        _player = player;
        _logger = logger;
    }

    private readonly IHostEnvironment _environment;
    private readonly ICardConfigs _cards;
    private readonly IBotConfig _bots;
    private readonly IGameModeConfig _gameMode;
    private readonly IMatchMakingConfig _matchMaking;
    private readonly IRatingConfig _rating;
    private readonly IInGameAchievementConfig _achievements;
    private readonly IUserDeckConfig _userDeck;
    private readonly IPlayerConfig _player;
    private readonly ILogger<GameConfigsSetup> _logger;

    public async Task OnCoordinatorSetupCompleted(IReadOnlyLifetime lifetime)
    {
        await ConfigsInitializer.Init(_environment, "config.cards", _cards, _logger);
        await ConfigsInitializer.Init(_environment, "config.bot", _bots, _logger);
        await ConfigsInitializer.Init(_environment, "config.gameMode", _gameMode, _logger);
        await ConfigsInitializer.Init(_environment, "config.matchMaking", _matchMaking, _logger);
        await ConfigsInitializer.Init(_environment, "config.rating", _rating, _logger);
        await ConfigsInitializer.Init(_environment, "config.achievements", _achievements, _logger);
        await ConfigsInitializer.Init(_environment, "config.userDeck", _userDeck, _logger);
        await ConfigsInitializer.Init(_environment, "config.player", _player, _logger);
    }
}
