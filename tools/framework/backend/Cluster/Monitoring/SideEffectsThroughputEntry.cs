namespace Cluster.Monitoring;

[GenerateSerializer]
public class SideEffectsThroughputEntry
{
    [Id(0)] public DateTime Timestamp { get; init; }
    [Id(1)] public long Processed { get; init; }
    [Id(2)] public long Failed { get; init; }
}
