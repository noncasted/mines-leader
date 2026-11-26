namespace Cluster.Monitoring;

[GenerateSerializer]
public class SideEffectsSnapshotResponse
{
    [Id(0)] public string ServiceTag { get; init; } = "";
    [Id(1)] public Guid ServiceId { get; init; }
    [Id(2)] public SideEffectsLiveData Data { get; init; } = new();
}
