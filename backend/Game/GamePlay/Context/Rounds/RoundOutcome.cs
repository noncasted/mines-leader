using Internal;

namespace Game.GamePlay;

public class RoundOutcome
{
    public RoundOutcome(IGameContext gameContext, RoundPlayers players)
    {
        _gameContext = gameContext;
        _players = players;
    }

    private readonly IGameContext _gameContext;
    private readonly RoundPlayers _players;

    public bool IsOver(IReadOnlyLifetime lifetime, IReadOnlyList<IPlayer> players, int roundsCount)
    {
        if (lifetime.IsTerminated == true)
            return true;

        if (players.Any(p => p.Health.Current == 0))
            return true;

        if (players.Any(p => p.User.Lifetime.IsTerminated == true))
            return true;

        if (roundsCount >= 2)
        {
            var flagWinner = _players.GetFlagWinner();

            if (flagWinner != Guid.Empty)
                return true;
        }

        return false;
    }

    // Guid.Empty when nobody lost by health or by a disconnect.
    public Guid FindDefeatWinner(IReadOnlyList<IPlayer> players)
    {
        foreach (var player in players)
        {
            if (player.Health.Current <= 0)
                return _gameContext.GetOpponent(player).User.Id;
        }

        foreach (var (user, player) in _gameContext.UserToPlayer)
        {
            if (user.Lifetime.IsTerminated == true)
                return _gameContext.GetOpponent(player).User.Id;
        }

        return Guid.Empty;
    }

    public string? FindDefeatReason(IReadOnlyList<IPlayer> players)
    {
        foreach (var player in players)
        {
            if (player.Health.Current <= 0)
                return $"Player {player.User.Id} health reached 0";
        }

        foreach (var (user, _) in _gameContext.UserToPlayer)
        {
            if (user.Lifetime.IsTerminated == true)
                return $"Player {user.Id} disconnected";
        }

        return null;
    }

    public string? FindFlagReason()
    {
        var flagWinner = _players.GetFlagWinner();

        if (flagWinner != Guid.Empty)
            return $"Player {flagWinner} flagged every mine on own board";

        return null;
    }

    public Guid GetWinner(IReadOnlyList<IPlayer> players)
    {
        var defeatWinner = FindDefeatWinner(players);

        if (defeatWinner != Guid.Empty)
            return defeatWinner;

        return _players.GetFlagWinner();
    }

    public string GetWinReason(IReadOnlyLifetime lifetime, IReadOnlyList<IPlayer> players)
    {
        var reason = FindDefeatReason(players) ?? FindFlagReason();

        if (reason != null)
            return reason;

        if (lifetime.IsTerminated == true)
            return "Match lifetime terminated";

        return "Unknown";
    }
}
