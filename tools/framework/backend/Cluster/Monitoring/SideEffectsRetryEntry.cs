namespace Cluster.Monitoring;

[GenerateSerializer]
public class SideEffectsRetryEntry
{
    [Id(0)] public Guid Id { get; init; }
    [Id(1)] public string TypeName { get; init; } = string.Empty;
    [Id(2)] public int RetryCount { get; init; }
    [Id(3)] public DateTime RetryAfter { get; init; }
    [Id(4)] public DateTime CreatedAt { get; init; }
}
