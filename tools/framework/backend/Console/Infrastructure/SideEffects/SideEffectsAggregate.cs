using Cluster.Monitoring;

namespace Console;

public class SideEffectsAggregate
{
    public int QueueCount { get; init; }
    public int ProcessingCount { get; init; }
    public int RetryCount { get; init; }
    public int DeadLetterCount { get; init; }
    public IReadOnlyList<PerServiceSideEffects> Services { get; init; } = [];
    public IReadOnlyList<SideEffectsThroughputEntry> AggregatedHistory { get; init; } = [];
    public IReadOnlyList<SideEffectsRetryEntry> RetryEntries { get; init; } = [];
    public DateTime UpdatedAt { get; init; }

    // Services that did not answer: the throughput lacks their part, the shared counters are unknown when nobody answered.
    public int MissingServices { get; init; }
    public bool HasCounts { get; init; }
}
