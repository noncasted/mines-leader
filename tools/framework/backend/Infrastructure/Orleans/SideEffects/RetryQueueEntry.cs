namespace Infrastructure;

public class RetryQueueEntry
{
    public required Guid Id { get; init; }
    public required string TypeName { get; init; }
    public required int RetryCount { get; init; }
    public required DateTime RetryAfter { get; init; }
    public required DateTime CreatedAt { get; init; }
}
