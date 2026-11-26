using Common.Extensions;
using Infrastructure.State;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Infrastructure;

// The database transaction of Transactions.Process: the writes, the callbacks, the seal of the participants, the commit.
internal class TransactionCommitter
{
    public TransactionCommitter(
        IDbSource dbSource,
        IEventStorage eventStorage,
        TransactionWriteBatch writeBatch,
        TransactionCommitStatus commitStatus,
        Func<NpgsqlTransaction, Task> commitTransaction,
        ILogger logger)
    {
        _dbSource = dbSource;
        _eventStorage = eventStorage;
        _writeBatch = writeBatch;
        _commitStatus = commitStatus;
        _commitTransaction = commitTransaction;
        _logger = logger;
    }

    private readonly IDbSource _dbSource;
    private readonly IEventStorage _eventStorage;
    private readonly TransactionWriteBatch _writeBatch;
    private readonly TransactionCommitStatus _commitStatus;
    private readonly Func<NpgsqlTransaction, Task> _commitTransaction;
    private readonly ILogger _logger;

    public async Task Commit(
        TransactionCommitResult result,
        TransactionContext context,
        List<Func<NpgsqlTransaction, Task>> callbacks)
    {
        await using var connection = await _dbSource.Value.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        string transactionXid;

        try
        {
            // Direct states and side effects go to Postgres as one NpgsqlBatch: one round trip
            // for the whole transaction instead of one per table group plus one for side effects.
            transactionXid = await _writeBatch.Write(connection, transaction, result.States, context.SideEffects);

            // Marten runs its own pipeline inside the same transaction; it cannot join the batch.
            if (result.Events.Count != 0)
                await _eventStorage.Write(transaction, result.Events);

            foreach (var callback in callbacks)
                await callback(transaction);

            // A participant taken over by another transaction (GrainTransactionOwner.Join) rejects the seal: the
            // upserts carry no version, committing would overwrite the newer commit with this old snapshot.
            await Task.WhenAll(context.Participants.Values.Select(participant => participant.Seal(context.Id)));
        }
        catch (Exception e)
        {
            _logger.LogError(e,
                "[Transaction] [Error] Failed to record changes during transaction {TransactionId}",
                context.Id);

            await transaction.RollbackAsync();
            throw;
        }

        // A failed commit is rolled back by the server; there is nothing left to roll back here. But the call can fail
        // after the server committed (the connection broke while the answer was on the way): reported as a failure,
        // the caller would apply the change again. The server tells what happened to the transaction id.
        try
        {
            await _commitTransaction(transaction);
        }
        catch (Exception e)
        {
            if (await _commitStatus.IsCommitted(transactionXid, e) == false)
                throw;

            _logger.LogWarning(e,
                "[Transaction] Commit of transaction {TransactionId} reported a failure, but the server committed it",
                context.Id);
        }
    }
}
