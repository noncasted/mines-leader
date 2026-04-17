using Game.Session;
using Microsoft.Extensions.Logging;

namespace Game.GamePlay;

public class GameCommandUtils
{
    public GameCommandUtils(
        IGameContext gameContext,
        IGameRound gameRound,
        IServiceProvider serviceProvider,
        ISnapshotSender snapshotSender,
        ISnapshotDiffGuard diffGuard,
        ILogger<GameCommandUtils> logger,
        ISessionLogger sessionLogger)
    {
        GameContext = gameContext;
        GameRound = gameRound;
        ServiceProvider = serviceProvider;
        SnapshotSender = snapshotSender;
        DiffGuard = diffGuard;
        Logger = logger;
        SessionLogger = sessionLogger;
    }

    public IGameContext GameContext { get; }
    public IGameRound GameRound { get; }
    public IServiceProvider ServiceProvider { get; }
    public ISnapshotSender SnapshotSender { get; }
    public ISnapshotDiffGuard DiffGuard { get; }
    public ILogger Logger { get; }
    public ISessionLogger SessionLogger { get; }
}