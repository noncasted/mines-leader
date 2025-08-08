using Game;
using Game.GamePlay;

namespace Context;

public interface IGameContext
{
    IReadOnlyList<IPlayer> Players { get; }
    IReadOnlyDictionary<IPlayer, IBoard> Boards { get; }
    IReadOnlyDictionary<IUser, IPlayer> UserToPlayer { get; }

    void AddPlayer(IPlayer player);
}

public class GameContext : IGameContext
{
    private readonly List<IPlayer> _players = new();
    private readonly Dictionary<IPlayer, IBoard> _boards = new();
    private readonly Dictionary<IUser, IPlayer>_userToPlayer = new();

    public IReadOnlyList<IPlayer> Players => _players;

    public IReadOnlyDictionary<IPlayer, IBoard> Boards => _boards;

    public IReadOnlyDictionary<IUser, IPlayer> UserToPlayer => _userToPlayer;

    public void AddPlayer(IPlayer player)
    {
        _players.Add(player);
        _boards.Add(player, player.Board);
        _userToPlayer.Add(player.User, player);
    }
}

public static class GameContextExtensions
{
    public static IPlayer GetOpponent(this IGameContext context, IPlayer player)
    {
        return context.UserToPlayer.First(t => t.Value != player).Value;
    }
}