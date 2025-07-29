using Context;

namespace Game.GamePlay;

public class GameCommandUtils
{
    public GameCommandUtils(IGameContext gameContext)
    {
        GameContext = gameContext;
    }

    public IGameContext GameContext { get; }
}