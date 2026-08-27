using Game.GamePlay.Snapshots;
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
        ISessionLogger sessionLogger,
        IMatchStatsTracker stats,
        IAgentObservationPublisher? observationPublisher = null)
    {
        GameContext = gameContext;
        GameRound = gameRound;
        ServiceProvider = serviceProvider;
        SnapshotSender = snapshotSender;
        DiffGuard = diffGuard;
        Logger = logger;
        SessionLogger = sessionLogger;
        Stats = stats;
        ObservationPublisher = observationPublisher;
    }

    public IGameContext GameContext { get; }
    public IGameRound GameRound { get; }
    public IServiceProvider ServiceProvider { get; }
    public ISnapshotSender SnapshotSender { get; }
    public ISnapshotDiffGuard DiffGuard { get; }
    public ILogger Logger { get; }
    public ISessionLogger SessionLogger { get; }
    public IMatchStatsTracker Stats { get; }
    public IAgentObservationPublisher? ObservationPublisher { get; }
}