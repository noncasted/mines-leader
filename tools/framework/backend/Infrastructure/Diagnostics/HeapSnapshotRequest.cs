namespace Infrastructure.Diagnostics;

[GenerateSerializer]
public class HeapSnapshotRequest
{
    [Id(0)] public bool Deep { get; init; }
}
