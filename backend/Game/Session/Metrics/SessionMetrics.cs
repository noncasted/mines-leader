using Game.GamePlay;
using Game.MatchMetrics;
using Internal;
using Shared;
using Meta.Matches;

namespace Game.Session;

// The stream of the server of a match (IMatchMetricsWriter, user Guid.Empty): what only the server sees - the connected
// humans (session.users, bots never connect), a human that joins (session.join) or leaves the running session
// (session.leave, the end of the session is not a leave) and the end of the round (session.end). The axis of the stream is
// the server time since its open (the framework); the seconds of the fields are the clock of the match (IMatchClock), the
// axis of the clients. One stream per session: a rematch is a new session with its own stream.
public class SessionMetrics : ISessionMetrics
{
    public SessionMetrics(
        IMatchMetricsWriter writer,
        ISessionData sessionData,
        ISessionUsers users,
        IExecutionQueue executionQueue,
        IMatchClock clock)
    {
        _writer = writer;
        _sessionData = sessionData;
        _users = users;
        _executionQueue = executionQueue;
        _clock = clock;
    }

    public const string UsersGauge = MatchGameMetrics.SessionUsers;
    public const string JoinEvent = MatchGameMetrics.SessionJoin;
    public const string LeaveEvent = MatchGameMetrics.SessionLeave;
    public const string EndEvent = MatchGameMetrics.SessionEnd;
    public const int NoWinner = -1;

    private readonly IMatchMetricsWriter _writer;
    private readonly ISessionData _sessionData;
    private readonly ISessionUsers _users;
    private readonly IExecutionQueue _executionQueue;
    private readonly IMatchClock _clock;

    // The index of every user at the start, bots included: a winner may have no connection by the end.
    private readonly Dictionary<Guid, int> _indices = new();

    private IMatchMetricsServerStream? _stream;
    private int _connected;

    private IMatchMetricsServerStream Stream => _stream ?? throw new InvalidOperationException("[Metrics] The session metrics have not started");
    private IReadOnlyLifetime Lifetime => _sessionData.Lifetime;

    public void Start()
    {
        if (_stream != null)
            throw new InvalidOperationException($"[Metrics] The session metrics of {_sessionData.Id} are started twice");

        foreach (var user in _users)
            _indices.Add(user.Id, user.Index);

        _stream = _writer.Open(Lifetime, _sessionData.Id);
        Stream.Gauge(UsersGauge, 0);

        // The users change in the queue of the session only.
        _executionQueue.Enqueue(() => _users.View(Lifetime, OnUser));
    }

    public void End(Guid winner)
    {
        if (Lifetime.IsTerminated == true)
            return;

        var winnerIndex = winner == Guid.Empty ? NoWinner : WinnerIndex(winner);
        Stream.Event(EndEvent, Number("seconds", Seconds()), Number("winner_index", winnerIndex));
    }

    private void OnUser(IReadOnlyLifetime userLifetime, IUser user)
    {
        if (user.IsBot == true)
            return;

        WriteUsers(+1);
        Stream.Event(JoinEvent, Number("index", user.Index));

        userLifetime.Listen(() => {
            if (Lifetime.IsTerminated == true)
                return;

            WriteUsers(-1);
            Stream.Event(LeaveEvent, Number("index", user.Index), Number("seconds", Seconds()));
        });
    }

    private int WinnerIndex(Guid winner)
    {
        if (_indices.TryGetValue(winner, out var index) == false)
            throw new KeyNotFoundException($"[Metrics] The winner {winner} is not a user of session {_sessionData.Id}");

        return index;
    }

    private void WriteUsers(int change)
    {
        _connected += change;
        Stream.Gauge(UsersGauge, _connected);
    }

    private double Seconds()
    {
        return Math.Round(_clock.Seconds, 1);
    }

    private static SharedMatchMetrics.MatchEventField Number(string key, double value)
    {
        return new SharedMatchMetrics.MatchEventField
        {
            Key = key,
            Number = value
        };
    }
}
