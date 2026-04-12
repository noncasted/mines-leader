namespace Game.GamePlay;

public interface IGameRandom
{
    bool FlipCoin(IPlayer player);
    int RollDice(IPlayer player, int sides);
    int Range(IPlayer player, int min, int max);
    int Index(IPlayer player, int count);
}

public class GameRandom : IGameRandom
{
    public bool FlipCoin(IPlayer player)
    {
        return Random.Shared.Next(2) == 0;
    }

    public int RollDice(IPlayer player, int sides)
    {
        return Random.Shared.Next(1, sides + 1);
    }

    public int Range(IPlayer player, int min, int max)
    {
        return Random.Shared.Next(min, max + 1);
    }

    public int Index(IPlayer player, int count)
    {
        return Random.Shared.Next(count);
    }
}