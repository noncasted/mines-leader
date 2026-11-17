namespace Infrastructure;

public class AdaptiveInterval
{
    public AdaptiveInterval(
        TimeSpan minInterval,
        TimeSpan maxInterval,
        TimeSpan failureBaseInterval,
        double jitterFactor = 0.2)
    {
        _minInterval = minInterval;
        _maxInterval = maxInterval;
        _failureBaseInterval = failureBaseInterval;
        _jitterFactor = jitterFactor;
    }

    private readonly TimeSpan _minInterval;
    private readonly TimeSpan _maxInterval;
    private readonly TimeSpan _failureBaseInterval;
    private readonly double _jitterFactor;

    private readonly object _gate = new();

    private int _consecutiveSuccesses;
    private int _consecutiveFailures;

    public void RecordSuccess()
    {
        lock (_gate)
        {
            _consecutiveSuccesses++;
            _consecutiveFailures = 0;
        }
    }

    public void RecordFailure()
    {
        lock (_gate)
        {
            _consecutiveFailures++;
            _consecutiveSuccesses = 0;
        }
    }

    public TimeSpan GetNextDelay()
    {
        int consecutiveSuccesses;
        int consecutiveFailures;

        lock (_gate)
        {
            consecutiveSuccesses = _consecutiveSuccesses;
            consecutiveFailures = _consecutiveFailures;
        }

        TimeSpan baseDelay;

        if (consecutiveFailures > 0)
        {
            var exponent = Math.Min(consecutiveFailures - 1, 5);
            var seconds = _failureBaseInterval.TotalSeconds * (1 << exponent);
            baseDelay = TimeSpan.FromSeconds(Math.Min(seconds, _maxInterval.TotalSeconds));
        }
        else
        {
            var step = Math.Min(consecutiveSuccesses, 10);
            var range = _maxInterval - _minInterval;
            baseDelay = _minInterval + TimeSpan.FromSeconds(range.TotalSeconds * step / 10);
        }

        var jitter = 1.0 + (Random.Shared.NextDouble() * 2 - 1) * _jitterFactor;
        return TimeSpan.FromMilliseconds(baseDelay.TotalMilliseconds * jitter);
    }
}