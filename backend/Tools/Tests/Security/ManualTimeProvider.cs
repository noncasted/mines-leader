namespace Tests.Security;

// A clock that moves only on Advance (ticket expiry, reservation expiry).
public class ManualTimeProvider : TimeProvider
{
    private readonly Lock _lock = new();
    private DateTimeOffset _now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow()
    {
        lock (_lock)
            return _now;
    }

    public void Advance(TimeSpan delta)
    {
        lock (_lock)
            _now += delta;
    }
}
