namespace Game.GamePlay;

public interface IBotContext
{
    IPlayer Bot { get; }
    IPlayer Opponent { get; }

    void Construct(IPlayer bot, IPlayer opponent);
}

public class BotContext : IBotContext
{
    public IPlayer Bot { get; private set; } = null!;
    public IPlayer Opponent { get; private set; } = null!;

    public void Construct(IPlayer bot, IPlayer opponent)
    {
        Bot = bot;
        Opponent = opponent;
    }
}