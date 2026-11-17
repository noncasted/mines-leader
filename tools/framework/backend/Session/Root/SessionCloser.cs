using Common.Extensions;
using Microsoft.Extensions.Logging;

namespace Game.Session;

// How a session ends: the end of a session left with nobody (the moment its admission says so, or a timer for the
// reservations nobody used) and the abort of one whose expected users have not come.
internal class SessionCloser
{
    public SessionCloser(
        SessionContainerData data,
        ISessionAdmission admission,
        IExecutionQueue executionQueue,
        TimeProvider time,
        ILogger logger)
    {
        _data = data;
        _admission = admission;
        _executionQueue = executionQueue;
        _time = time;
        _logger = logger;
    }

    private readonly SessionContainerData _data;
    private readonly ISessionAdmission _admission;
    private readonly IExecutionQueue _executionQueue;
    private readonly TimeProvider _time;
    private readonly ILogger _logger;

    // The session is already marked closed in the admission, so neither the search nor the gateway can
    // pick it. Termination is enqueued instead of being called in place: the caller may be in the middle
    // of a user removal, the other listeners of that removal must finish on a live session.
    public void Close()
    {
        _logger.LogInformation("[Session] Session {ID} closed: no users", _data.Id);
        _executionQueue.Enqueue(_data.Lifetime.Terminate);
    }

    // Runs in the queue. Closed first, as every other close: nobody reserves or enters a session that is being
    // terminated (a live reservation or a joined user would not let the admission close it by itself).
    public void Abort()
    {
        _admission.Close();
        _data.Lifetime.Terminate();
    }

    public void ScheduleEmptyCheck(TimeSpan delay)
    {
        Check().NoAwait();

        async Task Check()
        {
            await Task.Delay(delay, _time, _data.Lifetime.Token).ConfigureAwait(false);
            _executionQueue.Enqueue(CheckEmpty);
        }
    }

    private void CheckEmpty()
    {
        if (_admission.IsClosed == true)
            return;

        var now = _time.GetUtcNow().UtcDateTime;

        if (_admission.TryCloseIfEmpty(now) == true)
        {
            Close();
            return;
        }

        // The timer and the clock are different sources: a reservation that is still alive gets one more check.
        var timeout = _admission.GetReservationsTimeout(now);

        if (timeout != null)
            ScheduleEmptyCheck(timeout.Value);
    }
}
