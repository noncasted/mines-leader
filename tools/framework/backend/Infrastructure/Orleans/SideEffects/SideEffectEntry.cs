namespace Infrastructure;

public class SideEffectEntry
{
    public required Guid Id { get; init; }
    public required ISideEffect Effect { get; init; }
    public required int RetryCount { get; init; }
}
