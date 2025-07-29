using Game;
using Game.GamePlay;

namespace Context;

public interface IGameContext
{
    IReadOnlyDictionary<IPlayer, IBoard> Boards { get; }
    IReadOnlyDictionary<IUser, IPlayer> Players { get; }
}

public class GameContext : IGameContext
{
    public IReadOnlyDictionary<IPlayer, IBoard> Boards { get; }
    public IReadOnlyDictionary<IUser, IPlayer> Players { get; }
}

public static class GameContextExtensions
{
    public static IPlayer GetOpponent(this IGameContext context, IPlayer player)
    {
        return context.Players.First(t => t.Value != player).Value;
    }
}