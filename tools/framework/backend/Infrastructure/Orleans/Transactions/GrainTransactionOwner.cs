using Common.Extensions;
using Microsoft.Extensions.Logging;

namespace Infrastructure;

// Which transaction holds a grain: the lock, the wait for it and the takeover of a stuck transaction.
internal class GrainTransactionOwner
{
    public GrainTransactionOwner(ITransactionConfig transactionConfig, ILogger logger, Action rollbackStuck)
    {
        _transactionConfig = transactionConfig;
        _logger = logger;
        _rollbackStuck = rollbackStuck;
    }

    // Mutex held for the entire duration of an active transaction.
    // Initial count 1 = grain is free (unlocked).
    private readonly SemaphoreSlim _lock = new(1, 1);

    // The wait for the lock of each transaction that has one. Only the turns of the activation touch it.
    private readonly Dictionary<Guid, Task> _waits = new();

    private readonly ITransactionConfig _transactionConfig;
    private readonly ILogger _logger;
    private readonly Action _rollbackStuck;

    // Last activity time of the current transaction.
    // Refreshed on every Join() call (including idempotent re-joins) and on Seal().
    // Used to detect stuck transactions eligible for takeover.
    private DateTime _currentTransactionTime;

    // Id of the transaction currently holding this grain.
    // Guid.Empty means the grain is free.
    public Guid Current { get; private set; }

    public async Task Join(Guid transactionId)
    {
        // Same transaction calling again (e.g. multiple [Transaction] methods in one Process).
        // Refresh timestamp so the stuck-transaction check stays accurate.
        if (Current == transactionId)
        {
            Refresh();
            return;
        }

        // A parallel call of this transaction (a reentrant grain) already waits for the lock, and the lock comes once: a
        // wait of its own would be a wait for the transaction itself. It would fail, take the grain over from its own
        // transaction, or take the grain after the end of the transaction. This turn runs right behind the turn of the
        // first call: the transaction cannot end in between, it has not heard of either.
        if (_waits.TryGetValue(transactionId, out var pending) == true)
        {
            await pending;
            return;
        }

        // Grain is free — take the lock at once and register the new transaction. An empty Current does not say that:
        // a release passes the lock to a waiter, which registers itself in a later turn.
        if (_lock.Wait(0) == true)
        {
            Take(transactionId);
            return;
        }

        // Another transaction is active. Wait for it to finish.
        var wait = WaitAndTake(transactionId);
        _waits.Add(transactionId, wait);

        try
        {
            await wait;
        }
        finally
        {
            _waits.Remove(transactionId);
        }
    }

    public void Refresh()
    {
        _currentTransactionTime = DateTime.UtcNow;
    }

    public void Release()
    {
        Current = Guid.Empty;

        if (_lock.CurrentCount == 0)
            _lock.Release();
    }

    private async Task WaitAndTake(Guid transactionId)
    {
        // The previous transaction releases the lock in OnSuccess/OnFailure.
        var options = _transactionConfig.Value.ThrowIfNull();
        var isAcquired = await _lock.WaitAsync(TimeSpan.FromSeconds(options.LockWaitSeconds));

        if (isAcquired == false)
        {
            TakeOverOrThrow(transactionId, options);
            return;
        }

        // Acquired the lock normally — the previous transaction completed while we waited.
        // Sanity check: Current should be Guid.Empty at this point
        // (OnSuccess/OnFailure always clear it before releasing the lock).
        if (Current != Guid.Empty)
        {
            _lock.Release();

            throw new Exception(
                $"Handler failed to join transaction id '{transactionId}'. Current transaction in progress '{Current}'.");
        }

        Take(transactionId);
    }

    private void TakeOverOrThrow(Guid transactionId, TransactionOptions options)
    {
        // The holder released right after the wait ran out and nobody else waits.
        if (_lock.Wait(0) == true)
        {
            Take(transactionId);
            return;
        }

        // Timed out. If the stuck transaction is still within its grace period,
        // we cannot take over — it may still be running normally but slowly.
        // An empty Current is a released grain with the lock on its way to a waiter: it is that waiter's, nothing is stuck.
        if (Current == Guid.Empty || _currentTransactionTime.AddSeconds(options.StuckGraceSeconds) >= DateTime.UtcNow)
        {
            _logger.LogWarning(
                "[Transaction] [Join] Timeout waiting for lock. TransactionId={TransactionId} BlockedBy={BlockedBy}",
                transactionId, Current);

            throw new Exception(
                $"Handler failed to join transaction id '{transactionId}'. Current transaction in progress '{Current}'.");
        }

        // The transaction has been inactive for longer than StuckGraceSeconds — treat it as stuck.
        // Its lock passes to this transaction as it is: never released, so no waiter gets it in between and there is
        // nothing to wait for here; the waiters keep waiting for the end of this transaction.
        // The stuck transaction's eventual OnSuccess/OnFailure will fail the ID check
        // and exit early without touching the semaphore or state.
        var stuckAge = (DateTime.UtcNow - _currentTransactionTime).TotalSeconds;

        _logger.LogWarning(
            "[Transaction] [Takeover] Forcing takeover of stuck transaction. StuckTransactionId={StuckId} AgeSeconds={AgeSeconds} IncomingTransactionId={TransactionId}",
            Current, stuckAge, transactionId);

        // Roll back any in-memory state left by the stuck transaction. A rollback that throws leaves the grain to the
        // stuck transaction: the join fails with the reason, the next one tries again.
        _rollbackStuck();

        Take(transactionId);
    }

    private void Take(Guid transactionId)
    {
        Current = transactionId;
        Refresh();
    }
}
