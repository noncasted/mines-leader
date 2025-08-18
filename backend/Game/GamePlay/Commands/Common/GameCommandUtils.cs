using Microsoft.Extensions.Logging;

namespace Game.GamePlay;

public class GameCommandUtils
{
    public GameCommandUtils(
        IGameContext gameContext,
        IGameRound gameRound,
        ICardFactory cardFactory, 
        ISnapshotSender snapshotSender, 
        ILogger<GameCommandUtils> logger)
    {
        GameContext = gameContext;
        GameRound = gameRound;
        CardFactory = cardFactory;
        SnapshotSender = snapshotSender;
        Logger = logger;
    }

    public IGameContext GameContext { get; }
    public IGameRound GameRound { get; }
    public ICardFactory CardFactory { get; }
    public ISnapshotSender SnapshotSender { get; }
    public ILogger Logger { get; }
}