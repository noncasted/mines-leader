namespace Infrastructure;

public class SideEffectsStats
{
    public int QueueCount { get; init; }
    public int ProcessingCount { get; init; }
    public int RetryCount { get; init; }
    public int DeadLetterCount { get; init; }
}
