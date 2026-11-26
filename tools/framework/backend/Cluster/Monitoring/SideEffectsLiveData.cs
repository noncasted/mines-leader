namespace Cluster.Monitoring;

[GenerateSerializer]
public class SideEffectsLiveData
{
    [Id(0)] public int QueueCount { get; init; }
    [Id(1)] public int ProcessingCount { get; init; }
    [Id(2)] public int RetryCount { get; init; }
    [Id(3)] public int DeadLetterCount { get; init; }
    [Id(4)] public List<SideEffectsThroughputEntry> ThroughputHistory { get; init; } = [];
    [Id(5)] public List<SideEffectsRetryEntry> RetryEntries { get; init; } = [];
}
