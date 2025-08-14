using Microsoft.Extensions.Logging;

namespace Game.GamePlay;

public class GameCommandUtils
{
    public GameCommandUtils(
        IGameContext gameContext,
        ICardFactory cardFactory, 
        ISnapshotSender snapshotSender, 
        ILogger<GameCommandUtils> logger)
    {
        GameContext = gameContext;
        CardFactory = cardFactory;
        SnapshotSender = snapshotSender;
        Logger = logger;
    }

    public IGameContext GameContext { get; }
    public ICardFactory CardFactory { get; }
    public ISnapshotSender SnapshotSender { get; }
    public ILogger Logger { get; }
}