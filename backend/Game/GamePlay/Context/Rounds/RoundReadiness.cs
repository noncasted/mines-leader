using Cluster.Configs;
using Internal;

namespace Game.GamePlay;

public class RoundReadiness
{
    public RoundReadiness(
        IGameContext gameContext,
        IGameReadyAwaiter readyAwaiter,
        IPlayersReadyAwaiter playersReadyAwaiter,
        IPlayerConfig playerConfig)
    {
        _gameContext = gameContext;
        _readyAwaiter = readyAwaiter;
        _playersReadyAwaiter = playersReadyAwaiter;
        _playerConfig = playerConfig;
    }

    private readonly IGameContext _gameContext;
    private readonly IGameReadyAwaiter _readyAwaiter;
    private readonly IPlayersReadyAwaiter _playersReadyAwaiter;
    private readonly IPlayerConfig _playerConfig;

    public void PrepareHands()
    {
        foreach (var player in _gameContext.Players)
        {
            player.Hand.SetSize(_playerConfig.Value.HandSize);
            player.Deck.Init(_playerConfig.Value.DeckSize);
        }
    }

    public Task AwaitGame(IReadOnlyLifetime lifetime)
    {
        return _readyAwaiter.Await(lifetime);
    }

    public async Task AwaitPlayers(IReadOnlyLifetime lifetime)
    {
        var playersReadyLifetime = lifetime.Child();

        foreach (var player in _gameContext.Players)
            player.User.Lifetime.Listen(() => playersReadyLifetime.Terminate());

        await _playersReadyAwaiter.Await(playersReadyLifetime);
    }
}
