using Cluster.Monitoring;

namespace Console;

public class PerServiceSideEffects
{
    public string ServiceTag { get; init; } = "";
    public Guid ServiceId { get; init; }
    public SideEffectsLiveData Data { get; init; } = new();

    // The message of the request that failed; null: the service answered and Data holds its counts.
    public string? Failure { get; init; }
}
