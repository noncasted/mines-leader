namespace Infrastructure;

public class SideEffectsOptions
{
    public int ScanDelay { get; set; }
    public int ConcurrentExecutions { get; set; }
    public int MaxRetryCount { get; set; }
    public float IncrementalRetryDelay { get; set; }
}