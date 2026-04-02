namespace Infrastructure;

public class SideEffectsOptions
{
    public int ScanDelay { get; set; } = 5;              // ms between worker iterations when work was found
    public int EmptyScanDelay { get; set; } = 50;        // ms between worker iterations when queue is empty
    public int ConcurrentExecutions { get; set; } = 50;    // max parallel effects
    public int MaxRetryCount { get; set; } = 5;            // max attempts
    public float IncrementalRetryDelay { get; set; } = 30; // seconds * retryCount = delay
}

public interface ISideEffectsConfig : IAddressableState<SideEffectsOptions> { }
