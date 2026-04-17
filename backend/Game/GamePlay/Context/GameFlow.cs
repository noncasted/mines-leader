using Game.Session;
using Infrastructure;
using Meta.Matches;

namespace Game.GamePlay;

public interface IGameFlow
{
    Task<MatchTransitionResult> Process();
}

public class GameFlow : Service, IGameFlow
{
    public GameFlow(
        IOrleans orleans,
        IGameContext context,
        ISessionUsers users,
        IPlayerFactory playerFactory,
        ISessionData sessionData,
        IGameRound gameRound,
        MatchCreateOptions matchOptions,
        IRematchAwaiter rematchAwaiter,
        ISessionLogger sessionLogger,
        ISnapshotSender snapshotSender) : base("game-flow")
    {
        _orleans = orleans;
        _context = context;
        _users = users;
        _playerFactory = playerFactory;
        _sessionData = sessionData;
        _gameRound = gameRound;
        _matchOptions = matchOptions;
        _rematchAwaiter = rematchAwaiter;
        _sessionLogger = sessionLogger;
        _snapshotSender = snapshotSender;
    }

    private readonly IOrleans _orleans;
    private readonly IGameContext _context;
    private readonly ISessionUsers _users;
    private readonly IPlayerFactory _playerFactory;
    private readonly ISessionData _sessionData;
    private readonly IGameRound _gameRound;
    private readonly MatchCreateOptions _matchOptions;
    private readonly IRematchAwaiter _rematchAwaiter;
    private readonly ISessionLogger _sessionLogger;
    private readonly ISnapshotSender _snapshotSender;

    public async Task<MatchTransitionResult> Process()
    {
        var match = _orleans.GetGrain<IMatch>(_sessionData.Id);
        var playerIds = _users.Select(t => t.Id).ToList();

        _sessionLogger.LogSessionCreated(_sessionData.Type, _sessionData.Id);
        await _orleans.InTransaction(() => match.Setup(_matchOptions.Type, playerIds));

        foreach (var user in _users)
        {
            var player = await _playerFactory.Create(user);
            _context.AddPlayer(player);
        }

        _sessionLogger.RegisterPlayers(_users.ToList());
        _sessionLogger.LogGameStarted(playerIds);
        _context.OnGameStarted();
        var winner = await _gameRound.Process(_sessionData.Lifetime);

        var completionSnapshot = new MoveSnapshot();
        completionSnapshot.RecordGameCompleted(winner);
        _snapshotSender.Send(completionSnapshot);

        await _orleans.InTransaction(() => match.OnComplete(winner));

        var shouldRematch = await _rematchAwaiter.ShouldRematch(_sessionData.Lifetime, TimeSpan.FromSeconds(30));

        if (shouldRematch == true)
        {
            _sessionLogger.Log("[Game] Rematch requested");
            return MatchTransitionResult.Rematch;
        }

        return MatchTransitionResult.End;
    }
}