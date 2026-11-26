namespace Console.Monitoring;

public class HeapReport
{
    public required string FileName { get; init; }
    public required DateTime Timestamp { get; init; }
    public required long SizeBytes { get; init; }
}
