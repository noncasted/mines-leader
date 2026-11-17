using Infrastructure.State;

namespace Infrastructure;

internal class TransactionCommitResult
{
    public required IReadOnlyList<GrainStateRecord> States { get; init; }
    public required List<GrainEventRecord> Events { get; init; }
}
