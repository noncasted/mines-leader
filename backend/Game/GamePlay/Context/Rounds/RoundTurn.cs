using Internal;
using Game.Session;

namespace Game.GamePlay;

public class RoundTurn
{
    public RoundTurn(IGameContext gameContext, ISessionLogger sessionLogger)
    {
        _gameContext = gameContext;
        _sessionLogger = sessionLogger;
    }

    private readonly IGameContext _gameContext;
    private readonly ISessionLogger _sessionLogger;

    private readonly ViewableProperty<IPlayer> _currentPlayer = new(null!);

    private ILifetime? _forcedLifetime;

    public IViewableProperty<IPlayer> CurrentPlayer => _currentPlayer;

    public ILifetime Begin(IReadOnlyLifetime lifetime)
    {
        _forcedLifetime = lifetime.Child();
        return _forcedLifetime;
    }

    public void SetCurrent(IPlayer player)
    {
        _currentPlayer.Set(player);
    }

    public void Skip()
    {
        _sessionLogger.LogTurnSkipped(_currentPlayer.Value?.User.Id ?? Guid.Empty);
        _forcedLifetime!.Terminate();
    }

    public async Task TurnsCountdown(IPlayer player, ILifetime turnLifetime)
    {
        var timeSpan = TimeSpan.FromSeconds(0.2);

        while (player.Moves.Left > 0 && turnLifetime.IsTerminated == false)
            await Task.Delay(timeSpan, turnLifetime.Token);
    }

    public void ListenPlayers(IReadOnlyLifetime lifetime)
    {
        foreach (var player in _gameContext.Players)
        {
            player.Health.Updated.Advise(lifetime, () => {
                if (player.Health.Current > 0)
                    return;

                _forcedLifetime?.Terminate();
            });

            player.User.Lifetime.Listen(() => {
                Skip();

                if (lifetime.IsTerminated == true || _forcedLifetime == null)
                    return;

                _forcedLifetime.Terminate();
            });
        }
    }
}
