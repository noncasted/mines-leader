namespace Game.Session;

/// <summary>
/// S4: the places of a session. A user enters a session only with a live reservation of its own (the game gateway,
/// after the ticket of the meta is verified): the creator of a session reserves the places of its known users (a
/// match: its human participants; a rematch: the humans of the previous match), the lobby search reserves the place of
/// the user that asked. A reservation lives <see cref="SessionContainerData.JoinTimeout"/> and is used once: a second
/// connection of the same user needs a new reservation. Called from the gateway, the search and the session factory
/// threads, so it locks itself.
/// </summary>
public class SessionReservations
{
    public SessionReservations(TimeSpan timeout, TimeProvider time)
    {
        _timeout = timeout;
        _time = time;
    }

    private readonly TimeSpan _timeout;
    private readonly TimeProvider _time;
    private readonly Lock _lock = new();
    private readonly Dictionary<Guid, DateTime> _reservations = new();

    // A repeated reservation of the same user refreshes it.
    public void Reserve(Guid userId)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("A place is reserved for an empty user id", nameof(userId));

        lock (_lock)
            _reservations[userId] = Now() + _timeout;
    }

    // Takes the live reservation of the user: false when there is none (never made, expired, already used).
    public bool TryAccept(Guid userId)
    {
        lock (_lock)
        {
            if (_reservations.Remove(userId, out var expiresAt) == false)
                return false;

            return Now() < expiresAt;
        }
    }

    private DateTime Now()
    {
        return _time.GetUtcNow().UtcDateTime;
    }
}
