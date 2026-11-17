namespace Infrastructure.Diagnostics;

[GenerateSerializer]
public class HeapTypeEntry
{
    [Id(0)] public string TypeName { get; init; } = "";
    [Id(1)] public long Count { get; init; }
    [Id(2)] public long TotalBytes { get; init; }
}
