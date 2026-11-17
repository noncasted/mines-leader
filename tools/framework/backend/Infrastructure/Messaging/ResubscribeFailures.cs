namespace Infrastructure;

// The outcomes of the resubscribes of one listener: they set the next delay, and the log keeps the first failure in a
// row and every tenth.
internal class ResubscribeFailures
{
    public ResubscribeFailures(AdaptiveInterval interval, Action<Exception, int> logFailure)
    {
        Interval = interval;
        _logFailure = logFailure;
    }

    private readonly Action<Exception, int> _logFailure;

    private int _consecutiveFailures;

    public AdaptiveInterval Interval { get; }

    public void RecordSuccess()
    {
        Volatile.Write(ref _consecutiveFailures, 0);
        Interval.RecordSuccess();
    }

    public void RecordFailure(Exception e)
    {
        // The first Listen and the resubscribe loop record together.
        var failures = Interlocked.Increment(ref _consecutiveFailures);
        Interval.RecordFailure();

        if (failures == 1 || failures % 10 == 0)
            _logFailure(e, failures);
    }
}
