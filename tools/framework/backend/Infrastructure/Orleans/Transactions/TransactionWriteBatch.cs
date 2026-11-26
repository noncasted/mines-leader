using System.Collections.Concurrent;
using System.Diagnostics;
using Common.Extensions;
using Infrastructure.State;
using Npgsql;

namespace Infrastructure;

// The one NpgsqlBatch of a transaction: its id, the bound of the commit, the states and the side effects.
internal class TransactionWriteBatch
{
    public TransactionWriteBatch(
        IStateStorage stateStorage,
        ISideEffectsStorage sideEffectsStorage,
        Lazy<ITransactionConfig> transactionConfig)
    {
        _stateStorage = stateStorage;
        _sideEffectsStorage = sideEffectsStorage;
        _transactionConfig = transactionConfig;
    }

    private readonly IStateStorage _stateStorage;
    private readonly ISideEffectsStorage _sideEffectsStorage;
    private readonly Lazy<ITransactionConfig> _transactionConfig;

    // Returns the id of the database transaction (pg_current_xact_id) for TransactionCommitStatus.
    public async Task<string> Write(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        IReadOnlyList<GrainStateRecord> states,
        ConcurrentDictionary<Guid, ISideEffect> sideEffects)
    {
        // The server aborts this transaction when the commit does not follow the seal of the participants within
        // half of their stuck grace: a takeover after the grace (GrainTransactionHandler.Seal) cannot race a commit.
        var idleTimeout = (int)(_transactionConfig.Value.Value.StuckGraceSeconds * 1000 / 2);

        var commands = new List<NpgsqlBatchCommand>
        {
            new("SELECT pg_current_xact_id()::text"),
            new($"SET LOCAL idle_in_transaction_session_timeout = {idleTimeout}")
        };

        if (states.Count != 0)
            commands.AddRange(_stateStorage.BuildWriteCommands(states));

        if (sideEffects.Count != 0)
            commands.Add(_sideEffectsStorage.BuildWriteCommand(sideEffects.Values.ToList()));

        // Same metrics DirectStorage.Write would have recorded for the transactional write.
        var start = Stopwatch.GetTimestamp();

        try
        {
            await using var batch = new NpgsqlBatch(connection, transaction);

            foreach (var command in commands)
                batch.BatchCommands.Add(command);

            await using var reader = await batch.ExecuteReaderAsync();

            if (await reader.ReadAsync() == false)
                throw new InvalidOperationException("The batch of the transaction returned no transaction id");

            var transactionXid = reader.GetString(0);

            // The errors of the writes come with their results.
            while (await reader.NextResultAsync())
            {
            }

            return transactionXid;
        }
        finally
        {
            if (states.Count != 0)
            {
                StateTelemetry.WriteDuration.Record(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
                StateTelemetry.WriteTotal.Add(1);
                StateTelemetry.WriteBatchSize.Record(states.Count);
            }
        }
    }
}
