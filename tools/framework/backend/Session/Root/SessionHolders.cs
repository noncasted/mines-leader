namespace Game.Session;

// Who holds a place in a session: live reservations, users in the middle of joining, joined connections, away seats.
// Not thread safe and without the state of the session: guarded by the lock of SessionAdmission, which owns the rule.
internal class SessionHolders
{
    public SessionHolders(TimeSpan joinTimeout, TimeSpan reconnectWindow)
    {
        _joinTimeout = joinTimeout;
        _reconnectWindow = reconnectWindow;
    }

    private readonly TimeSpan _joinTimeout;
    private readonly TimeSpan _reconnectWindow;

    private readonly Dictionary<Guid, DateTime> _reservations = new();
    private readonly Dictionary<Guid, int> _joining = new();

    // Away seats by user: the end of their reconnect window. Removed by a return (its TryStartJoining or its join) or by
    // TryExpireAway only.
    private readonly Dictionary<Guid, DateTime> _away = new();

    // Joined connections by user: a reconnecting user may hold two for a moment (the stale one is being dropped).
    private readonly Dictionary<Guid, int> _joined = new();

    private int _joiningCount;
    private int _joinedCount;

    public bool IsEmpty => _joinedCount == 0 && _joiningCount == 0 && _reservations.Count == 0 && _away.Count == 0;

    // No expired reservations: removed by the caller. Neither the seal nor the close: the state of the session.
    public SessionReservation CheckReserve(Guid userId, int maxUsers)
    {
        if (IsHeldOnlyBy(userId) == true)
            return SessionReservation.HeldOnlyByOwnStale;

        if (_reservations.ContainsKey(userId) == true || CountHoldersExcept(userId) < maxUsers)
            return SessionReservation.Reserved;

        return SessionReservation.Full;
    }

    public void Reserve(Guid userId, DateTime now)
    {
        _reservations[userId] = now + _joinTimeout;
    }

    public bool TryStartJoining(Guid userId, bool allowsReturn)
    {
        var isReserved = _reservations.Remove(userId);
        var isReturn = allowsReturn == true && (_away.Remove(userId) == true || _joined.ContainsKey(userId) == true);

        if (isReserved == false && isReturn == false)
            return false;

        _joining[userId] = _joining.GetValueOrDefault(userId) + 1;
        _joiningCount++;

        return true;
    }

    public void Join(Guid userId)
    {
        // A user may join without TryAccept (tests): then it only consumes its own reservation, if there is one.
        if (RemoveJoining(userId) == false)
            _reservations.Remove(userId);

        // A return accepted while its old connection was still joined: the old one may be lost before this join, and
        // its seat is back here, not away. The timer of that window finds no away mark of it and ends.
        _away.Remove(userId);

        _joined[userId] = _joined.GetValueOrDefault(userId) + 1;
        _joinedCount++;
    }

    public void MarkAway(Guid userId, DateTime now)
    {
        if (_joined.ContainsKey(userId) == false)
            throw new InvalidOperationException($"User {userId} is away from a session it has not joined");

        if (_away.ContainsKey(userId) == true)
            throw new InvalidOperationException($"User {userId} is already away");

        if (_reconnectWindow == TimeSpan.Zero)
            throw new InvalidOperationException($"User {userId} is away from a session without a reconnect window");

        _away.Add(userId, now + _reconnectWindow);
    }

    public bool IsAway(Guid userId)
    {
        return _away.ContainsKey(userId);
    }

    public bool TryExpireAway(Guid userId, DateTime now)
    {
        if (_away.TryGetValue(userId, out var expiresAt) == false || expiresAt > now)
            return false;

        _away.Remove(userId);
        return true;
    }

    public bool IsJoined(Guid userId)
    {
        return _joined.ContainsKey(userId);
    }

    public void Leave(Guid userId)
    {
        if (_joined.TryGetValue(userId, out var count) == false)
            throw new InvalidOperationException($"User {userId} left a session it has not joined");

        if (count == 1)
            _joined.Remove(userId);
        else
            _joined[userId] = count - 1;

        _joinedCount--;
    }

    public bool RemoveJoining(Guid userId)
    {
        if (_joining.TryGetValue(userId, out var count) == false)
            return false;

        if (count == 1)
            _joining.Remove(userId);
        else
            _joining[userId] = count - 1;

        _joiningCount--;
        return true;
    }

    public bool CancelReservation(Guid userId)
    {
        return _reservations.Remove(userId);
    }

    public IReadOnlyList<Guid> GetPendingUsers()
    {
        var users = new List<Guid>(_reservations.Keys);

        foreach (var userId in _joining.Keys)
        {
            if (users.Contains(userId) == false)
                users.Add(userId);
        }

        return users;
    }

    public TimeSpan? GetReservationsTimeout(DateTime now)
    {
        if (_reservations.Count == 0)
            return null;

        return _reservations.Values.Max() - now;
    }

    public void RemoveExpired(DateTime now)
    {
        foreach (var (userId, expiresAt) in _reservations)
        {
            if (expiresAt <= now)
                _reservations.Remove(userId);
        }
    }

    private bool IsHeldOnlyBy(Guid userId)
    {
        return _joined.ContainsKey(userId) == true && CountHoldersExcept(userId) == 0;
    }

    // The joined connections of the user itself are stale: they neither take a place nor hold the session.
    private int CountHoldersExcept(Guid userId)
    {
        var stale = _joined.GetValueOrDefault(userId);
        var awayOthers = _away.Count - (_away.ContainsKey(userId) == true ? 1 : 0);

        return _joinedCount - stale + _joiningCount + _reservations.Count + awayOthers;
    }
}
