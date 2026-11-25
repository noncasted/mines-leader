using Cluster.Configs;
using Common.Extensions;
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
        IBotConfig botConfig,
        ISnapshotSender snapshotSender,
        IAgentObservationPublisher observationPublisher,
        IMatchStatsTracker statsTracker,
        IMatchClock clock,
        ISessionMetrics sessionMetrics) : base("game-flow")
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
        _botConfig = botConfig;
        _snapshotSender = snapshotSender;
        _observationPublisher = observationPublisher;
        _statsTracker = statsTracker;
        _clock = clock;
        _sessionMetrics = sessionMetrics;
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
    private readonly IBotConfig _botConfig;
    private readonly ISnapshotSender _snapshotSender;
    private readonly IAgentObservationPublisher _observationPublisher;
    private readonly IMatchStatsTracker _statsTracker;
    private readonly IMatchClock _clock;
    private readonly ISessionMetrics _sessionMetrics;

    private bool _isCloseOnEndListened;

    public async Task<MatchTransitionResult> Process()
    {
        var match = _orleans.GetGrain<IMatch>(_sessionData.Id);
        var playerIds = _users.Select(t => t.Id).ToList();

        // Players are known by their session ids, the meta by the user ids (S3). Taken at the start: a user that
        // leaves is removed from the session users, its result still goes to the meta.
        var ids = new MatchUserIds(_users);

        _sessionLogger.LogSessionCreated(_sessionData.Type, _sessionData.Id);
        await _orleans.InTransaction(() => match.Setup(_matchOptions.Type, ids.ToBackend(playerIds)));
        ListenCloseOnEnd(match);

        foreach (var user in _users)
        {
            var player = await _playerFactory.Create(user);
            _context.AddPlayer(player);
        }

        _sessionLogger.RegisterPlayers(_users.ToList());
        _sessionLogger.LogGameStarted(playerIds);

        foreach (var user in _users.Where(u => u.IsBot))
            _sessionLogger.LogBotProfile(user.Id, MatchBotProfile.Resolve(_matchOptions, _botConfig).ToString());

        // The clock of the match goes before the first snapshot of the game (sent by the listeners of the start).
        _clock.Start();
        _sessionMetrics.Start();
        _context.OnGameStarted();
        var winner = await _gameRound.Process(_sessionData.Lifetime);
        _sessionMetrics.End(winner);

        var matchStats = _statsTracker.Collect();

        // The match results are computed by the grain, and the client needs them on the results screen,
        // so the match is finished before the end-of-game snapshot is sent.
        var summary = winner != Guid.Empty
            ? ids.ToSession(await _orleans.InTransaction(() =>
                match.OnComplete(ids.ToBackend(winner), ids.ToBackend(matchStats))))
            : null;

        if (winner == Guid.Empty)
            await _orleans.InTransaction(match.Close);

        var completionSnapshot = new MoveSnapshot();
        completionSnapshot.RecordGameCompleted(winner, playerIds, matchStats, summary, _context.Boards.Values);
        _snapshotSender.Send(completionSnapshot);

        foreach (var user in _users)
        {
            if (user.IsBot)
                continue;

            _observationPublisher.Publish(user.Id, "game_over", false, string.Empty);
        }

        var shouldRematch = await _rematchAwaiter.ShouldRematch(_sessionData.Lifetime, TimeSpan.FromSeconds(30));

        if (shouldRematch == true)
        {
            _sessionLogger.Log("[Game] Rematch requested");
            return MatchTransitionResult.Rematch;
        }

        return MatchTransitionResult.End;
    }

    // A session that ends during a round (everybody left, the server stops) closes its match without a winner; a match
    // closed already stays as it is. A rematch is a new session with its own record (MatchHandle).
    private void ListenCloseOnEnd(IMatch match)
    {
        if (_isCloseOnEndListened == true)
            return;

        _isCloseOnEndListened = true;
        _sessionData.Lifetime.Listen(() => _orleans.InTransaction(match.Close).NoAwait());
    }
}

// The two ids of the users of a match (IUser.Id in the session, IUser.BackendId in the meta) at its start.
public class MatchUserIds
{
    public MatchUserIds(IEnumerable<IUser> users)
    {
        foreach (var user in users)
        {
            _toBackend.Add(user.Id, user.BackendId);
            _toSession.Add(user.BackendId, user.Id);
        }
    }

    private readonly Dictionary<Guid, Guid> _toBackend = new();
    private readonly Dictionary<Guid, Guid> _toSession = new();

    public Guid ToBackend(Guid sessionId)
    {
        if (_toBackend.TryGetValue(sessionId, out var backendId) == false)
            throw new KeyNotFoundException($"No user {sessionId} in the match");

        return backendId;
    }

    public List<Guid> ToBackend(IEnumerable<Guid> sessionIds)
    {
        return sessionIds.Select(ToBackend).ToList();
    }

    public Dictionary<Guid, Meta.Users.UserStatsDelta> ToBackend(Dictionary<Guid, Meta.Users.UserStatsDelta> stats)
    {
        return stats.ToDictionary(entry => ToBackend(entry.Key), entry => entry.Value);
    }

    public MatchCompletionSummary ToSession(MatchCompletionSummary summary)
    {
        return new MatchCompletionSummary
        {
            Duration = summary.Duration,
            RatingChanges = summary.RatingChanges.ToDictionary(entry => ToSession(entry.Key), entry => entry.Value),
            Ratings = summary.Ratings.ToDictionary(entry => ToSession(entry.Key), entry => entry.Value)
        };
    }

    private Guid ToSession(Guid backendId)
    {
        if (_toSession.TryGetValue(backendId, out var sessionId) == false)
            throw new KeyNotFoundException($"No meta user {backendId} in the match");

        return sessionId;
    }
}
