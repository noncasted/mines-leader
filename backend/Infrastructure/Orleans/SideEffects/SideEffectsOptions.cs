namespace Infrastructure;

public class SideEffectsOptions
{
    public int ScanDelay { get; set; } = 500;              // ms between worker iterations
    public int ConcurrentExecutions { get; set; } = 10;    // max parallel effects
    public int MaxRetryCount { get; set; } = 5;            // max attempts
    public float IncrementalRetryDelay { get; set; } = 30; // seconds * retryCount = delay
}
