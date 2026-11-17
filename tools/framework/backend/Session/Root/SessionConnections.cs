using Internal;
using Microsoft.Extensions.Logging;

namespace Game.Session;

// What a connection of a user means for its seat and for the session: a new one replaces the stale ones of its user,
// the end of one is a leave, a loss or nothing (a replaced connection), a user that comes anew drops what it had here.
internal class SessionConnections
{
    public SessionConnections(
        SessionContainerData data,
        SessionCreateOptions options,
        ISessionUsers users,
        ISessionSeats seats,
        ISessionAdmission admission,
        ISessionPresence presence,
        SessionAwaySeats awaySeats,
        SessionCloser closer,
        IExecutionQueue executionQueue,
        TimeProvider time,
        ILogger logger)
    {
        _data = data;
        _options = options;
        _users = users;
        _seats = seats;
        _admission = admission;
        _presence = presence;
        _awaySeats = awaySeats;
        _closer = closer;
        _executionQueue = executionQueue;
        _time = time;
        _logger = logger;
    }

    private readonly SessionContainerData _data;
    private readonly SessionCreateOptions _options;
    private readonly ISessionUsers _users;
    private readonly ISessionSeats _seats;
    private readonly ISessionAdmission _admission;
    private readonly ISessionPresence _presence;
    private readonly SessionAwaySeats _awaySeats;
    private readonly SessionCloser _closer;
    private readonly IExecutionQueue _executionQueue;
    private readonly TimeProvider _time;
    private readonly ILogger _logger;

    // Runs in the execution queue (users are added and removed only there).
    public void OnUserAdded(IReadOnlyLifetime userLifetime, IUser user)
    {
        // A bot is connected as long as the session and never holds it.
        if (user.IsBot == true)
        {
            userLifetime.Listen(() => _seats.Detach(user));
            return;
        }

        _admission.OnUserJoined(user.BackendId);

        userLifetime.Listen(() => OnConnectionEnded(user));

        // A user connects once: an older connection of the same user is stale (its client was lost without a close
        // and the reservation did not drop it in time). Dropped after the listeners of this addition.
        var stale = _users.Where(other => other != user && other.BackendId == user.BackendId).ToList();

        if (stale.Count == 0)
            return;

        _executionQueue.Enqueue(() => {
            foreach (var other in stale)
                Disconnect(other, "the user connected again");
        });
    }

    public void DropConnections(Guid userId)
    {
        if (_admission.IsJoined(userId) == false)
            return;

        // FIFO: the drop runs before any connection the user opens after this call. A user that comes anew has left
        // its seat here: no reconnect window.
        _executionQueue.Enqueue(() => {
            foreach (var user in _users.Where(user => user.IsBot == false && user.BackendId == userId).ToList())
            {
                if (user.Seat.Connected == user)
                    user.Seat.MarkLeaving(user);

                Disconnect(user, "the user came anew");
            }
        });
    }

    // A returning user that has not made it: its seat has no connection and is not away any more (TryAccept took
    // it), nothing would end it. It leaves.
    public void EndFailedReturn(Guid userId)
    {
        _executionQueue.Enqueue(() => {
            if (_data.Lifetime.IsTerminated == true || _seats.Entries.TryGetValue(userId, out var seat) == false)
                return;

            if (seat.Connected != null || _admission.IsAway(userId) == true)
                return;

            _logger.LogWarning("[Session] Session {ID}: user {Index} {UserId} failed to return to its seat: it leaves",
                _data.Id,
                seat.Index,
                userId);

            _presence.EndSeat(seat);
        });
    }

    // Runs in the queue: the connection of the user is removed from the users (its lifetime has ended).
    private void OnConnectionEnded(IUser user)
    {
        var isCurrent = _seats.Detach(user);
        var now = _time.GetUtcNow().UtcDateTime;

        // A connection replaced by a newer one of the same seat (a return before its loss was noticed) ends silently;
        // the end of the session ends every seat by itself.
        if (isCurrent == true && _data.Lifetime.IsTerminated == false)
        {
            var seat = user.Seat;

            // The user is gone for good: the others destroy its entities with it (the backend ends them with the seat).
            if (seat.IsLeaving == true || _options.IsReconnectable == false)
                _presence.EndSeat(seat);
            else
                _awaySeats.MarkAway(seat, now);
        }

        if (_admission.OnUserLeft(user.BackendId, now) == true)
            _closer.Close();
    }

    // The connection is closed; the user is removed as after any disconnect (UserFactory: the end of the connection
    // ends the user, also before the session runs the connection).
    private void Disconnect(IUser user, string reason)
    {
        _logger.LogInformation("[Session] Session {ID}: stale connection of user {Index} {UserId} dropped: {Reason}",
            _data.Id,
            user.Index,
            user.BackendId,
            reason);

        user.Connection.Disconnect();
    }
}
