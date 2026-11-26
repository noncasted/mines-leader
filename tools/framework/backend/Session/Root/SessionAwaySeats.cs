using Common.Extensions;
using Microsoft.Extensions.Logging;

namespace Game.Session;

// The seats of the users that lost their connection without a leave: the seat waits for its user for the reconnect
// window and is ended when the window is over.
internal class SessionAwaySeats
{
    public SessionAwaySeats(
        SessionContainerData data,
        SessionCreateOptions options,
        ISessionAdmission admission,
        ISessionPresence presence,
        SessionCloser closer,
        IExecutionQueue executionQueue,
        TimeProvider time,
        ILogger logger)
    {
        _data = data;
        _options = options;
        _admission = admission;
        _presence = presence;
        _closer = closer;
        _executionQueue = executionQueue;
        _time = time;
        _logger = logger;
    }

    private readonly SessionContainerData _data;
    private readonly SessionCreateOptions _options;
    private readonly ISessionAdmission _admission;
    private readonly ISessionPresence _presence;
    private readonly SessionCloser _closer;
    private readonly IExecutionQueue _executionQueue;
    private readonly TimeProvider _time;
    private readonly ILogger _logger;

    // Runs in the queue, before the admission is told the connection has left: the away seat keeps holding the session.
    public void MarkAway(ISessionSeat seat, DateTime now)
    {
        _admission.OnUserAway(seat.UserId, now);

        _logger.LogInformation(
            "[Session] Session {ID}: user {Index} {UserId} lost its connection, its seat waits {Window} s",
            _data.Id,
            seat.Index,
            seat.UserId,
            _options.ReconnectWindow.TotalSeconds);

        _presence.MarkAway(seat);

        ScheduleExpiry(seat, now + _options.ReconnectWindow);
    }

    private void ScheduleExpiry(ISessionSeat seat, DateTime windowEnd)
    {
        var delay = windowEnd - _time.GetUtcNow().UtcDateTime;

        Check().NoAwait();

        async Task Check()
        {
            await Task.Delay(delay, _time, seat.Lifetime.Token).ConfigureAwait(false);
            _executionQueue.Enqueue(() => Expire(seat, windowEnd));
        }
    }

    private void Expire(ISessionSeat seat, DateTime windowEnd)
    {
        if (_data.Lifetime.IsTerminated == true || seat.Lifetime.IsTerminated == true)
            return;

        var now = _time.GetUtcNow().UtcDateTime;

        // The timer and the clock are different sources: the rest of the window.
        if (now < windowEnd)
        {
            ScheduleExpiry(seat, windowEnd);
            return;
        }

        // Returned (or returning: TryAccept has taken the away mark), or away again: a later loss has a timer of its own.
        if (_admission.TryExpireAway(seat.UserId, now) == false)
            return;

        if (seat.Connected != null)
            throw new InvalidOperationException($"Seat {seat.Index} {seat.UserId} is away with a connection");

        _logger.LogInformation("[Session] Session {ID}: user {Index} {UserId} has not returned in time: it leaves",
            _data.Id,
            seat.Index,
            seat.UserId);

        _presence.EndSeat(seat);

        if (_admission.TryCloseIfEmpty(_time.GetUtcNow().UtcDateTime) == true)
            _closer.Close();
    }
}
