using Infrastructure.State;

namespace Infrastructure;

[GenerateSerializer]
public class TransactionHandlerResult
{
    [Id(0)]
    public required IReadOnlyList<IStateValue> States { get; init; }

    [Id(1)]
    public required List<GrainEventRecord> Events { get; init; }

    // Monotonic per handler: a later CollectResult on the same participant has a bigger Sequence.
    [Id(2)]
    public long Sequence { get; init; }
}
