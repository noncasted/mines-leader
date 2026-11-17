namespace Infrastructure.Diagnostics;

[GenerateSerializer]
public class HeapSnapshotResponse
{
    [Id(0)] public string ServiceName { get; init; } = "";
    [Id(1)] public Guid ServiceId { get; init; }
    [Id(2)] public DateTime Timestamp { get; init; }
    [Id(3)] public long WorkingSetBytes { get; init; }
    [Id(4)] public long GcTotalBytes { get; private set; }
    [Id(5)] public long Gen0SizeBytes { get; private set; }
    [Id(6)] public long Gen1SizeBytes { get; private set; }
    [Id(7)] public long Gen2SizeBytes { get; private set; }
    [Id(8)] public long LohSizeBytes { get; private set; }
    [Id(9)] public long PohSizeBytes { get; private set; }
    [Id(10)] public IReadOnlyList<HeapTypeEntry> TopTypes { get; private set; } = Array.Empty<HeapTypeEntry>();
    [Id(11)] public string? Error { get; private set; }
    [Id(12)] public long CollectDurationMs { get; private set; }
    [Id(13)] public bool Deep { get; init; }

    public void SetGcMemory(long totalBytes, ReadOnlySpan<GCGenerationInfo> generations)
    {
        GcTotalBytes = totalBytes;

        if (generations.Length > 0) Gen0SizeBytes = generations[0].SizeAfterBytes;
        if (generations.Length > 1) Gen1SizeBytes = generations[1].SizeAfterBytes;
        if (generations.Length > 2) Gen2SizeBytes = generations[2].SizeAfterBytes;
        if (generations.Length > 3) LohSizeBytes = generations[3].SizeAfterBytes;
        if (generations.Length > 4) PohSizeBytes = generations[4].SizeAfterBytes;
    }

    public void SetTopTypes(IReadOnlyList<HeapTypeEntry> topTypes)
    {
        TopTypes = topTypes;
    }

    public void SetError(string error)
    {
        Error = error;
    }

    public void SetCollectDuration(long milliseconds)
    {
        CollectDurationMs = milliseconds;
    }
}
