using Context;

namespace Game.GamePlay;

public class GameCommandUtils
{
    public GameCommandUtils(
        IGameContext gameContext,
        ICardFactory cardFactory, 
        ISnapshotSender snapshotSender)
    {
        GameContext = gameContext;
        CardFactory = cardFactory;
        SnapshotSender = snapshotSender;
    }

    public IGameContext GameContext { get; }
    public ICardFactory CardFactory { get; }
    public ISnapshotSender SnapshotSender { get; }
}