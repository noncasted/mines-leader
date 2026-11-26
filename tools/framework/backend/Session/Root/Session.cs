using Internal;
using Microsoft.Extensions.Logging;
using Shared;

namespace Game.Session;

public interface ISession
{
    Guid Id { get; }
    SessionType Type { get; }

    // A private session is never returned by the search: the only way in is a reservation made by its
    // creator (lobby code, lobby start).
    bool IsPrivate { get; }

    // No place can be reserved or taken any more; the lifetime is terminated a moment later, from the queue.
    bool IsClosed { get; }

    // No new reservations: the lobby has started its match (see ISessionAdmission.IsSealed).
    bool IsSealed { get; }

    IReadOnlyLifetime Lifetime { get; }
    IUserFactory UserFactory { get; }
    ISessionUsers Users { get; }
    IExecutionQueue ExecutionQueue { get; }

    // Completes once the session handles its users (all the expected ones have joined, see Run), from any thread and
    // also for a wait that begins after it; cancelled when the session ends before that.
    Task AllUsersConnected { get; }

    // Search, lobby: reserves a place for the user, otherwise the reason (sealed, closed, full - SessionCreateOptions.MaxUsers).
    // The user that gets a place comes anew: its connections still held here are stale and are dropped
    // (DropConnections); a session held only by them is refused (HeldOnlyByOwnStale) and they are dropped too, so the
    // session closes. Any other refusal leaves a live connection of the user alone.
    SessionReservation Reserve(Guid userId);

    // Any thread: the user comes anew (a search, a reservation), so its connections still held by this session belong
    // to a client lost without a close (a domain reload in the editor, a crash) - they are dropped in the queue.
    // A session left with nobody is closed as after any last disconnect.
    void DropConnections(Guid userId);

    // Any thread: the session is not closed and the user would get a place (joined, joining and reserved
    // users are counted, except the stale connections of the user itself).
    bool HasFreePlace(Guid userId);

    // Lobby: the user took a place in another lobby, its reservation here (if any) is dropped. A session
    // left with nobody is closed at once.
    void CancelReservation(Guid userId);

    // Gateway: false if the session is closed or the user holds no live reservation (ISessionAdmission.TryAccept);
    // in a session with a reconnect window the user may also return to its seat (away within its window, or still
    // joined). After true the caller must either create the user (in the execution queue) or report OnJoinFailed.
    bool TryAccept(Guid userId);
    void OnJoinFailed(Guid userId);

    Task Run();
}

// The session as the rest of the server sees it: a composer over the parts of its life (who holds it, when it starts
// handling its users, what the end of a connection means, how it closes) and the admission rule they share.
public class Session : ISession
{
    public Session(
        SessionContainerData data,
        SessionCreateOptions options,
        IUserFactory userFactory,
        ISessionUsers users,
        ISessionSeats seats,
        IExecutionQueue executionQueue,
        ISessionAdmission admission,
        ISessionPresence presence,
        TimeProvider time,
        ILogger<Session> logger)
    {
        _data = data;
        _options = options;
        _users = users;
        _admission = admission;
        _time = time;
        ExecutionQueue = executionQueue;
        UserFactory = userFactory;

        _closer = new SessionCloser(data, admission, executionQueue, time, logger);

        var awaySeats = new SessionAwaySeats(data, options, admission, presence, _closer, executionQueue, time, logger);
        _connections = new SessionConnections(data, options, users, seats, admission, presence, awaySeats, _closer, executionQueue, time, logger);

        users.View(Lifetime, _connections.OnUserAdded);

        var userRuns = new SessionUserRuns(data, presence, executionQueue, logger);
        _start = new SessionStart(data, options, users, userRuns, _closer, executionQueue, time, logger);
    }

    private readonly ISessionUsers _users;
    private readonly ISessionAdmission _admission;
    private readonly TimeProvider _time;
    private readonly SessionContainerData _data;
    private readonly SessionCreateOptions _options;
    private readonly SessionCloser _closer;
    private readonly SessionConnections _connections;
    private readonly SessionStart _start;

    public Guid Id => _data.Id;
    public SessionType Type => _options.Type;
    public bool IsPrivate => _options.IsPrivate;
    public bool IsClosed => _admission.IsClosed;
    public bool IsSealed => _admission.IsSealed;
    public IReadOnlyLifetime Lifetime => _data.Lifetime;

    public IUserFactory UserFactory { get; }
    public ISessionUsers Users => _users;
    public IExecutionQueue ExecutionQueue { get; }
    public Task AllUsersConnected => _start.AllUsersConnected;

    public SessionReservation Reserve(Guid userId)
    {
        var reservation = _admission.Reserve(userId, _options.MaxUsers, GetNow());

        // The stale connections of the user are all that holds the session: dropped, it closes.
        if (reservation == SessionReservation.HeldOnlyByOwnStale)
            DropConnections(userId);

        if (reservation != SessionReservation.Reserved)
            return reservation;

        // After the reservation, still before the user can connect with it: the drop runs before the new connection
        // in the queue (FIFO).
        DropConnections(userId);

        _closer.ScheduleEmptyCheck(_options.JoinTimeout);
        return reservation;
    }

    public void DropConnections(Guid userId)
    {
        _connections.DropConnections(userId);
    }

    public bool HasFreePlace(Guid userId)
    {
        return _admission.HasFreePlace(userId, _options.MaxUsers, GetNow());
    }

    public void CancelReservation(Guid userId)
    {
        if (_admission.CancelReservation(userId, GetNow()) == true)
            _closer.Close();
    }

    public bool TryAccept(Guid userId)
    {
        return _admission.TryAccept(userId, GetNow(), _options.IsReconnectable);
    }

    public void OnJoinFailed(Guid userId)
    {
        if (_admission.OnJoinFailed(userId, GetNow()) == true)
        {
            _closer.Close();
            return;
        }

        _connections.EndFailedReturn(userId);
    }

    // Completes when the users are handled (a session with ExpectedUsers - once all of them have joined) or when the
    // session ends before that.
    public Task Run()
    {
        // A session that nobody reserves or enters (Create with ExpectedUsers) is closed after JoinTimeout.
        _closer.ScheduleEmptyCheck(_options.JoinTimeout);

        return _start.Run();
    }

    private DateTime GetNow()
    {
        return _time.GetUtcNow().UtcDateTime;
    }
}
