using Infrastructure.State;
using Microsoft.Extensions.Logging;

namespace Infrastructure;

// GrainTransactionHandler is attached to every grain as an IGrainExtension.
// It serializes access between concurrent transactions on the same grain: GrainTransactionOwner holds the grain for
// one transaction, GrainTransactionChanges keeps what that transaction changed.
//
// Invariant: the lock of the owner is held for the entire duration of an active transaction.
//   - Acquired in Join() when a new transaction registers this grain
//   - Released in OnSuccess() / OnFailure() when the transaction ends, except a rollback that throws (see OnFailure)
//
// All methods are [AlwaysInterleave], meaning Orleans can call them concurrently
// even if the grain itself is busy — hence the semaphore is required.
//
// Transaction lifecycle on this grain:
//   Transactions.Process() calls grain methods
//     → TransactionAttribute intercepts each outgoing call
//       → Join() is called on the target grain's handler (registers participant)
//       → grain method executes, calls State.Write() → RecordStateChanged()
//   After all grain calls finish:
//     → CollectResult() — snapshots current in-memory state for DB write
//     → [DB write happens atomically in a single Postgres transaction]
//     → Seal() — checks the grain still belongs to the transaction, right before the DB commit
//     → OnSuccess() — confirms commit, releases lock
//   On any exception:
//     → OnFailure() — rolls back in-memory state, releases lock
public class GrainTransactionHandler : IGrainTransactionHandler
{
    public GrainTransactionHandler(
        ITransactionConfig transactionConfig,
        ILogger<GrainTransactionHandler> logger)
    {
        _logger = logger;
        _owner = new GrainTransactionOwner(transactionConfig, logger, Rollback);
    }

    // Stable identity returned to Transactions.Process() to track this grain as a participant.
    private readonly Guid _participantId = Guid.NewGuid();

    private readonly GrainTransactionChanges _changes = new();
    private readonly GrainTransactionOwner _owner;
    private readonly ILogger<GrainTransactionHandler> _logger;

    // Called by TransactionAttribute every time a [Transaction] method on this grain is invoked.
    // Returns _participantId so Transactions.Process() can track this grain.
    public async Task<Guid> Join(Guid transactionId)
    {
        await _owner.Join(transactionId);
        return _participantId;
    }

    public void RecordStateChanged(IGrainStateTransactionParticipant state)
    {
        _changes.Record(state);
    }

    public void RecordEventStateChanged(IGrainEventTransactionParticipant events)
    {
        _changes.Record(events);
    }

    public void RecordEventStateChanged(IGrainEventTransactionParticipant events, IReadOnlyList<EventPayload> payloads)
    {
        _changes.Record(events, payloads);
    }

    // Called by Transactions.Process() after all grain methods have executed.
    // Returns the current in-memory snapshots of all modified states.
    // These are then written atomically to Postgres in a single DB transaction.
    public Task<TransactionHandlerResult> CollectResult(Guid transactionId)
    {
        if (_owner.Current != transactionId)
        {
            _logger.LogError(
                "[Transaction] [CollectResult] Transaction ID mismatch. Expected={TransactionId} Current={CurrentId}",
                transactionId, _owner.Current);

            throw new Exception(
                $"Handler failed to complete transaction id '{transactionId}'. Current transaction in progress '{_owner.Current}'.");
        }

        return Task.FromResult(_changes.Snapshot());
    }

    // A transaction taken over (Join) is rejected here, before its DB commit. A sealed transaction gets a new
    // StuckGraceSeconds: TransactionWriteBatch bounds the time between the seal and the commit by the
    // idle_in_transaction_session_timeout of its DB transaction, so a takeover after that grace cannot race the commit.
    public Task Seal(Guid transactionId)
    {
        if (_owner.Current != transactionId)
        {
            throw new Exception(
                $"Transaction '{transactionId}' lost the grain before its commit. Current transaction '{_owner.Current}'.");
        }

        _owner.Refresh();
        return Task.CompletedTask;
    }

    // Called by Transactions.Process() after the DB write succeeds.
    // Confirms the commit to each state object and releases the lock.
    public Task OnSuccess(Guid transactionId)
    {
        // ID mismatch means this grain was taken over by another transaction.
        // That transaction now owns the lock — do not release it here.
        if (_owner.Current != transactionId)
        {
            _logger.LogWarning(
                "[Transaction] [OnSuccess] Skipped — grain was taken over. ExpectedId={TransactionId} CurrentId={CurrentId}",
                transactionId, _owner.Current);

            return Task.CompletedTask;
        }

        // The transaction is committed: every participant is confirmed and the grain is released even when
        // one of them throws, otherwise the next transaction waits for the stuck-transaction takeover.
        var failures = _changes.Confirm();
        _owner.Release();

        if (failures != null)
            throw new AggregateException($"Participants failed to confirm committed transaction '{transactionId}'", failures);

        return Task.CompletedTask;
    }

    // Called by Transactions.Process() on any exception (before or after DB write).
    // Rolls back in-memory state and releases the lock.
    // A participant that throws does not stop the others, but the grain stays held: the memory of that participant is
    // unknown, and the next transaction would read it and could commit it. OnSuccess differs: there the memory is what
    // the database holds. The takeover in GrainTransactionOwner.Join is the next attempt for the ones that threw.
    public Task OnFailure(Guid transactionId)
    {
        // ID mismatch means this grain was taken over — nothing to roll back here.
        if (_owner.Current != transactionId)
            return Task.CompletedTask;

        Rollback();
        _owner.Release();

        return Task.CompletedTask;
    }

    private void Rollback()
    {
        var failures = _changes.Rollback();

        if (failures != null)
            throw new AggregateException($"Participants failed to roll back transaction '{_owner.Current}'", failures);
    }
}
