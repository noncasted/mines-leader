using Game.GamePlay.Snapshots;
using Game.Session;

namespace Game.GamePlay;

public class RoundSnapshots
{
    public RoundSnapshots(
        IGameContext gameContext,
        ISnapshotSender snapshotSender,
        ISnapshotDiffGuard diffGuard,
        ISessionLogger sessionLogger)
    {
        _gameContext = gameContext;
        _snapshotSender = snapshotSender;
        _diffGuard = diffGuard;
        _sessionLogger = sessionLogger;
    }

    private readonly IGameContext _gameContext;
    private readonly ISnapshotSender _snapshotSender;
    private readonly ISnapshotDiffGuard _diffGuard;
    private readonly ISessionLogger _sessionLogger;

    public void SendChecked(string stage, Action<MoveSnapshot> record)
    {
        var snapshot = new MoveSnapshot(_sessionLogger);

        var preState = _diffGuard.IsEnabled == true
            ? GameStateCapture.Capture(_gameContext)
            : null;

        record(snapshot);

        if (preState != null)
        {
            var postState = GameStateCapture.Capture(_gameContext);
            _diffGuard.Validate(preState, snapshot.Collect(), postState, stage);
        }

        _snapshotSender.Send(snapshot);
    }

    public void Send(Action<MoveSnapshot> record)
    {
        var snapshot = new MoveSnapshot(_sessionLogger);
        record(snapshot);
        _snapshotSender.Send(snapshot);
    }
}
