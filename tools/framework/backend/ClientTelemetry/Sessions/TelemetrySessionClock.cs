namespace Game.ClientTelemetry;

// The time of the sessions, apart from the TimeProvider of the host: the runtime of a silo runs its own timers on that one.
public sealed class TelemetrySessionClock
{
    public TelemetrySessionClock(TimeProvider time)
    {
        _time = time;
    }

    private readonly TimeProvider _time;

    public DateTime UtcNow => _time.GetUtcNow().UtcDateTime;
}
