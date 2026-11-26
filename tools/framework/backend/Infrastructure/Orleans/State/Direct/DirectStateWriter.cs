using Common;
using Common.Extensions;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Infrastructure.State;

// Runs the commands of a write or a delete, inside the transaction of the caller or in its own.
internal class DirectStateWriter
{
    public DirectStateWriter(IDbSource dbSource, DirectStateCommands commands, ILogger logger)
    {
        _dbSource = dbSource;
        _commands = commands;
        _logger = logger;
    }

    private readonly IDbSource _dbSource;
    private readonly DirectStateCommands _commands;
    private readonly ILogger _logger;

    public async Task Write(StateWriteRequest request)
    {
        using var watch = MetricWatch.Start(StateTelemetry.WriteDuration);

        var records = request.Records;

        try
        {
            var commands = _commands.BuildWriteCommands(records);
            await Execute(request.Transaction, commands);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[DirectStorage] Failed to write {Count} records", records.Count);
            throw;
        }
        finally
        {
            StateTelemetry.WriteTotal.Add(1);
            StateTelemetry.WriteBatchSize.Record(records.Count);
        }
    }

    public async Task Delete(StateDeleteRequest request)
    {
        var identities = request.Identities;

        if (identities.Count == 0)
            return;

        using var watch = MetricWatch.Start(StateTelemetry.DeleteDuration);

        try
        {
            var commands = DirectStateCommands.BuildDeleteCommands(identities);
            await Execute(request.Transaction, commands);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[DirectStorage] Failed to delete {Count} records", identities.Count);
            throw;
        }
        finally
        {
            StateTelemetry.DeleteTotal.Add(1);
        }
    }

    // One command per (table, extension) group. A single INSERT ... ON CONFLICT is atomic on its own,
    // so a single command runs without BEGIN/COMMIT (one round trip instead of three).
    // Several commands (several tables) still need an explicit transaction for cross-table atomicity.
    private async Task Execute(NpgsqlTransaction? transaction, IReadOnlyList<NpgsqlBatchCommand> commands)
    {
        if (commands.Count == 0)
            return;

        if (transaction != null)
        {
            await ExecuteBatch(transaction.Connection!, transaction, commands);
            return;
        }

        await using var connection = await _dbSource.Value.OpenConnectionAsync();

        if (commands.Count == 1)
        {
            await ExecuteBatch(connection, null, commands);
            return;
        }

        await using var localTransaction = await connection.BeginTransactionAsync();

        try
        {
            await ExecuteBatch(connection, localTransaction, commands);
            await localTransaction.CommitAsync();
        }
        catch
        {
            await localTransaction.RollbackAsync();
            throw;
        }
    }

    private static async Task ExecuteBatch(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        IReadOnlyList<NpgsqlBatchCommand> commands)
    {
        await using var batch = new NpgsqlBatch(connection, transaction);

        foreach (var command in commands)
            batch.BatchCommands.Add(command);

        await batch.ExecuteNonQueryAsync();
    }
}
