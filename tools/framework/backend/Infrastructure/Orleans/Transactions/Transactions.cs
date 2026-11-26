using System.Diagnostics;
using Common.Extensions;
using Infrastructure.State;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Infrastructure;

public interface ITransactions
{
    Task<TransactionResult> Process(TransactionParameters action);
}

// The steps of one transaction: the action, the snapshots of the participants, the database commit, the end of the
// participants. What each step does lives in TransactionParticipants and TransactionCommitter.
public class Transactions : ITransactions
{
    public Transactions(
        IStateStorage stateStorage,
        IEventStorage eventStorage,
        IDbSource dbSource,
        ISideEffectsStorage sideEffectsStorage,
        Lazy<ITransactionConfig> transactionConfig,
        ILogger<Transactions> logger)
    {
        _logger = logger;
        _participants = new TransactionParticipants(logger);

        _committer = new TransactionCommitter(
            dbSource,
            eventStorage,
            new TransactionWriteBatch(stateStorage, sideEffectsStorage, transactionConfig),
            new TransactionCommitStatus(dbSource),
            CommitTransaction,
            logger);
    }

    private readonly ILogger<Transactions> _logger;
    private readonly TransactionParticipants _participants;
    private readonly TransactionCommitter _committer;

    public async Task<TransactionResult> Process(TransactionParameters parameters)
    {
        using var activity = TransactionTelemetry.Source.StartActivity("Transaction.Process");
        using var watch = MetricWatch.Start(TransactionTelemetry.Duration);

        var context = new TransactionContext
        {
            Id = Guid.NewGuid()
        };

        activity?.SetTag("transaction.id", context.Id);

        TransactionContextProvider.SetCurrent(context);

        try
        {
            await parameters.Action();
        }
        catch (Exception e)
        {
            _logger.LogError(e,
                "[Transaction] [Error] In action exception occured during transaction {TransactionId}",
                context.Id);

            return await Fail(context, activity, e);
        }

        TransactionCommitResult result;

        try
        {
            result = await _participants.Collect(context);
        }
        catch (Exception e)
        {
            _logger.LogError(e,
                "[Transaction] [Error] Failed to collect commit result during transaction {TransactionId}",
                context.Id);

            return await Fail(context, activity, e);
        }

        activity?.SetTag("participant.count", context.Participants.Count);
        TransactionTelemetry.ParticipantCount.Record(context.Participants.Count);

        try
        {
            await _committer.Commit(result, context, parameters.Callbacks);
        }
        catch (TransactionOutcomeUnknownException e)
        {
            // Neither a success nor a failure the caller may retry: the memory of the participants is reloaded from
            // the database (whatever is there), the caller gets the exception.
            _logger.LogError(e,
                "[Transaction] [Error] Outcome of the commit of transaction {TransactionId} is unknown",
                context.Id);

            await Fail(context, activity, e);
            throw;
        }
        catch (Exception e)
        {
            _logger.LogError(e,
                "[Transaction] [Error] Failed to commit to db during transaction {TransactionId}",
                context.Id);

            return await Fail(context, activity, e);
        }

        // The changes are in the database from here on: the result is success whatever the participants do.
        // Rolling back (OnFailure) after the commit would desync grain memory from the database, and a failure
        // result would make the caller retry an operation that is already done.
        await _participants.Confirm(context);

        TransactionTelemetry.Total.Add(1);
        TransactionTelemetry.Success.Add(1);

        return new TransactionResult
        {
            IsSuccess = true
        };
    }

    // The commit itself, apart for the tests: a commit that throws after the server committed.
    protected virtual Task CommitTransaction(NpgsqlTransaction transaction)
    {
        return transaction.CommitAsync();
    }

    private async Task<TransactionResult> Fail(TransactionContext context, Activity? activity, Exception error)
    {
        activity?.SetStatus(ActivityStatusCode.Error, "Transaction rolled back");
        TransactionTelemetry.Rollback.Add(1);

        await _participants.Rollback(context);

        TransactionTelemetry.Total.Add(1);
        TransactionTelemetry.Failure.Add(1);

        return new TransactionResult
        {
            IsSuccess = false,
            Error = error
        };
    }
}
