namespace Tests.Game;

// A clock whose time and timestamps move only on Advance (the clock of a match).
public class SteppedTimeProvider : TimeProvider
{
    private readonly Lock _lock = new();
    private DateTimeOffset _now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override DateTimeOffset GetUtcNow()
    {
        lock (_lock)
            return _now;
    }

    public override long GetTimestamp()
    {
        return GetUtcNow().UtcTicks;
    }

    public void Advance(TimeSpan delta)
    {
        lock (_lock)
            _now += delta;
    }
}
