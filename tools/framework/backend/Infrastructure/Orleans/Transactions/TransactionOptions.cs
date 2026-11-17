using Common;
using Shared;

namespace Infrastructure;

[GrainState(Table = "configs", State = "transaction_config", Lookup = "TransactionConfig", Key = GrainKeyType.String)]
// A record: the console edits a config with `with` on its own copy.
public record TransactionOptions
{
    public float LockWaitSeconds { get; init; } = 3f; // how long to wait for another transaction to release the lock

    public float StuckGraceSeconds { get; init; } =
        30f; // grace period before a non-responsive transaction is considered stuck
}
