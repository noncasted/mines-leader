namespace Game.Session;

// Admission rule of a session: who may still enter it and when it is closed.
// A session is closed once it has no joined users, no users in the middle of joining, no live
// reservations and no away seats. Bots (IUser.IsBot) are not counted: they never hold a session. The closed flag is set in the same critical section that counts users and reservations,
// so the search can never reserve a place in a session that is being closed.
// Synchronous, no timers inside: the caller passes the current time.
public interface ISessionAdmission
{
    bool IsClosed { get; }

    // No new reservations (Reserve is refused, a live reservation is not refreshed either); the users that
    // hold a reservation still enter. A lobby that has started its match.
    bool IsSealed { get; }

    void Seal();

    // The match of the lobby was not created: the lobby takes reservations again.
    void Unseal();

    // Closes the session whoever still holds it (the expected users have not all joined in time): the caller
    // terminates it right after.
    void Close();

    // Search, lobby: reserves a place for SessionCreateOptions.JoinTimeout. A repeated call from the same user refreshes the reservation.
    // A user that reserves a place is not connected any more: its joined connections are stale (the client was lost
    // without a close; the session drops them, see ISession.DropConnections). They don't take a place, and a session
    // held only by them is refused (HeldOnlyByOwnStale) - it is closed as soon as they are dropped.
    SessionReservation Reserve(Guid userId, int maxUsers, DateTime now);

    // Not closed, not sealed and the user would get a place now (Reserve would succeed).
    bool HasFreePlace(Guid userId, int maxUsers, DateTime now);

    // Gateway, on connect: the live reservation of the user turns into a joining user that keeps holding the
    // session (no entry without one - the limit and the seal are checked by Reserve). False when the session is
    // closed or the user has no live reservation (never made, expired, already used by another connection).
    // allowsReturn (a session with a reconnect window): the user may also return to its seat without a reservation - a seat away
    // (OnUserAway, within its window) or a seat whose connection is still joined (the client has seen the loss before
    // the server; the old connection is dropped by the session). Neither the limit nor the seal apply: the place is
    // its own.
    bool TryAccept(Guid userId, DateTime now, bool allowsReturn);

    // The last connection of the user is lost without a leave in a session with a reconnect window. Its seat keeps
    // holding the session until now + ReconnectWindow; called before OnUserLeft of that connection.
    void OnUserAway(Guid userId, DateTime now);

    bool IsAway(Guid userId);

    // The window of the away seat is over: the seat is dropped here (the session ends the seat itself). False while
    // the window lasts or when the user is not away (it has returned).
    bool TryExpireAway(Guid userId, DateTime now);

    void OnUserJoined(Guid userId);

    // The user has at least one joined connection.
    bool IsJoined(Guid userId);

    // The following three return true when this very call has closed the session.
    bool OnUserLeft(Guid userId, DateTime now);
    bool OnJoinFailed(Guid userId, DateTime now);
    bool TryCloseIfEmpty(DateTime now);

    // Drops the reservation of the user, if it has one (the user took a place in another lobby). True when
    // this very call has closed the session.
    bool CancelReservation(Guid userId, DateTime now);

    // Users that hold a place without being joined: live reservations and users in the middle of joining.
    IReadOnlyList<Guid> GetPendingUsers(DateTime now);

    // Time until the last live reservation expires, null when there are none.
    TimeSpan? GetReservationsTimeout(DateTime now);
}

// The rule over the holders of the session: one lock, so the closed flag is set in the same critical section that
// counts them.
public class SessionAdmission : ISessionAdmission
{
    public SessionAdmission(SessionCreateOptions options)
    {
        _holders = new SessionHolders(options.JoinTimeout, options.ReconnectWindow);
    }

    private readonly SessionHolders _holders;
    private readonly Lock _lock = new();

    private bool _isClosed;
    private bool _isSealed;

    public bool IsClosed
    {
        get
        {
            lock (_lock)
                return _isClosed;
        }
    }

    public bool IsSealed
    {
        get
        {
            lock (_lock)
                return _isSealed;
        }
    }

    public void Seal()
    {
        lock (_lock)
            _isSealed = true;
    }

    public void Unseal()
    {
        lock (_lock)
            _isSealed = false;
    }

    public void Close()
    {
        lock (_lock)
            _isClosed = true;
    }

    public SessionReservation Reserve(Guid userId, int maxUsers, DateTime now)
    {
        lock (_lock)
        {
            if (_isSealed == true)
                return SessionReservation.Sealed;

            if (_isClosed == true)
                return SessionReservation.Closed;

            _holders.RemoveExpired(now);

            var reservation = _holders.CheckReserve(userId, maxUsers);

            if (reservation == SessionReservation.Reserved)
                _holders.Reserve(userId, now);

            return reservation;
        }
    }

    public bool HasFreePlace(Guid userId, int maxUsers, DateTime now)
    {
        lock (_lock)
        {
            if (_isClosed == true || _isSealed == true)
                return false;

            _holders.RemoveExpired(now);

            return _holders.CheckReserve(userId, maxUsers) == SessionReservation.Reserved;
        }
    }

    public bool TryAccept(Guid userId, DateTime now, bool allowsReturn)
    {
        lock (_lock)
        {
            if (_isClosed == true)
                return false;

            _holders.RemoveExpired(now);

            return _holders.TryStartJoining(userId, allowsReturn);
        }
    }

    public void OnUserJoined(Guid userId)
    {
        lock (_lock)
        {
            if (_isClosed == true)
                throw new InvalidOperationException($"User {userId} joined a closed session");

            _holders.Join(userId);
        }
    }

    public void OnUserAway(Guid userId, DateTime now)
    {
        lock (_lock)
        {
            if (_isClosed == true)
                throw new InvalidOperationException($"User {userId} is away from a closed session");

            _holders.MarkAway(userId, now);
        }
    }

    public bool IsAway(Guid userId)
    {
        lock (_lock)
            return _holders.IsAway(userId);
    }

    public bool TryExpireAway(Guid userId, DateTime now)
    {
        lock (_lock)
            return _holders.TryExpireAway(userId, now);
    }

    public bool IsJoined(Guid userId)
    {
        lock (_lock)
            return _holders.IsJoined(userId);
    }

    public bool OnUserLeft(Guid userId, DateTime now)
    {
        lock (_lock)
        {
            _holders.Leave(userId);
            return CloseIfEmpty(now);
        }
    }

    public bool OnJoinFailed(Guid userId, DateTime now)
    {
        lock (_lock)
        {
            if (_holders.RemoveJoining(userId) == false)
                throw new InvalidOperationException($"User {userId} is not joining the session");

            return CloseIfEmpty(now);
        }
    }

    public bool TryCloseIfEmpty(DateTime now)
    {
        lock (_lock)
            return CloseIfEmpty(now);
    }

    public bool CancelReservation(Guid userId, DateTime now)
    {
        lock (_lock)
        {
            if (_holders.CancelReservation(userId) == false)
                return false;

            return CloseIfEmpty(now);
        }
    }

    public IReadOnlyList<Guid> GetPendingUsers(DateTime now)
    {
        lock (_lock)
        {
            _holders.RemoveExpired(now);

            return _holders.GetPendingUsers();
        }
    }

    public TimeSpan? GetReservationsTimeout(DateTime now)
    {
        lock (_lock)
        {
            _holders.RemoveExpired(now);

            return _holders.GetReservationsTimeout(now);
        }
    }

    private bool CloseIfEmpty(DateTime now)
    {
        if (_isClosed == true)
            return false;

        _holders.RemoveExpired(now);

        if (_holders.IsEmpty == false)
            return false;

        _isClosed = true;
        return true;
    }
}
