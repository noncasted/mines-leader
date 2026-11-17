namespace Infrastructure;

// What the resubscribe loop of a client needs from a listener of any transport.
internal interface IResubscribingListener
{
    AdaptiveInterval Interval { get; }

    Task Resubscribe();
    void RecordSuccess();
    void RecordFailure(Exception e);
}
