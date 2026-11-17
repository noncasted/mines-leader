namespace Game.GamePlay;

public class MatchClock : IMatchClock
{
    public MatchClock(TimeProvider time)
    {
        _time = time;
    }

    private readonly TimeProvider _time;

    private long? _startedAt;

    public long Ms => (long)Elapsed.TotalMilliseconds;
    public double Seconds => Elapsed.TotalSeconds;

    private TimeSpan Elapsed => _startedAt == null ? TimeSpan.Zero : _time.GetElapsedTime(_startedAt.Value);

    public void Start()
    {
        if (_startedAt != null)
            throw new InvalidOperationException("[MatchClock] The clock of the match is started twice");

        _startedAt = _time.GetTimestamp();
    }
}
