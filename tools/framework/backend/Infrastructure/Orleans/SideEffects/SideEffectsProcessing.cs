using Common;
using Common.Extensions;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Infrastructure;

// The outcome of an entry a worker holds in side_effects_processing: done, failed or still running.
internal class SideEffectsProcessing
{
    public SideEffectsProcessing(IDbSource dbSource, ILogger logger)
    {
        _dbSource = dbSource;
        _logger = logger;
    }

    private readonly IDbSource _dbSource;
    private readonly ILogger _logger;

    public async Task CompleteProcessing(NpgsqlTransaction transaction, Guid id)
    {
        await using var command = transaction.Connection!.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "DELETE FROM side_effects_processing WHERE id = @id";
        command.Parameters.AddWithValue("id", id);
        await command.ExecuteNonQueryAsync();
    }

    public async Task CompleteProcessing(Guid id)
    {
        await using var connection = await _dbSource.Value.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM side_effects_processing WHERE id = @id";
        command.Parameters.AddWithValue("id", id);
        await command.ExecuteNonQueryAsync();
    }

    public async Task FailProcessing(
        Guid id,
        int retryCount,
        int maxAttempts,
        float incrementalRetryDelaySeconds,
        string? errorMessage = null)
    {
        // retryCount counts the failed attempts before this one.
        var attempt = retryCount + 1;

        if (attempt >= maxAttempts)
        {
            await using var connection = await _dbSource.Value.OpenConnectionAsync();
            await using var deadLetterTx = await connection.BeginTransactionAsync();

            await using var insertCmd = connection.CreateCommand();
            insertCmd.Transaction = deadLetterTx;

            insertCmd.CommandText = $@"
                INSERT INTO {SideEffectTables.DeadLetter} (id, payload, retry_count, created_at, failed_at, error_message)
                SELECT id, payload, retry_count, created_at, now(), @errorMessage
                FROM side_effects_processing
                WHERE id = @id
                ON CONFLICT (id) DO NOTHING
            ";
            insertCmd.Parameters.AddWithValue("id", id);
            insertCmd.Parameters.AddWithValue("errorMessage", (object?)errorMessage ?? DBNull.Value);
            await insertCmd.ExecuteNonQueryAsync();

            await using var deleteCmd = connection.CreateCommand();
            deleteCmd.Transaction = deadLetterTx;
            deleteCmd.CommandText = "DELETE FROM side_effects_processing WHERE id = @id";
            deleteCmd.Parameters.AddWithValue("id", id);
            await deleteCmd.ExecuteNonQueryAsync();

            await deadLetterTx.CommitAsync();

            SideEffectTelemetry.DeadLetter.Add(1);

            _logger.LogError("[SideEffects] Effect {Id} moved to dead letter after {Attempts} attempts: {Error}",
                id, attempt, errorMessage);
            return;
        }

        var delaySeconds = attempt * incrementalRetryDelaySeconds;
        var retryAfter = DateTime.UtcNow.AddSeconds(delaySeconds);
        var newRetryCount = attempt;

        await using var conn = await _dbSource.Value.OpenConnectionAsync();
        await using var tx = await conn.BeginTransactionAsync();

        await using var insertCommand = conn.CreateCommand();
        insertCommand.Transaction = tx;

        insertCommand.CommandText = @"
            INSERT INTO side_effects_retry_queue (id, payload, retry_count, created_at, retry_after)
            SELECT id, payload, @newRetryCount, created_at, @retryAfter
            FROM side_effects_processing
            WHERE id = @id
        ";
        insertCommand.Parameters.AddWithValue("id", id);
        insertCommand.Parameters.AddWithValue("newRetryCount", newRetryCount);
        insertCommand.Parameters.AddWithValue("retryAfter", retryAfter);
        await insertCommand.ExecuteNonQueryAsync();

        await using var deleteCommand = conn.CreateCommand();
        deleteCommand.Transaction = tx;
        deleteCommand.CommandText = "DELETE FROM side_effects_processing WHERE id = @id";
        deleteCommand.Parameters.AddWithValue("id", id);
        await deleteCommand.ExecuteNonQueryAsync();

        await tx.CommitAsync();
    }

    public async Task TouchProcessing(IReadOnlyCollection<Guid> ids)
    {
        if (ids.Count == 0)
            return;

        await using var connection = await _dbSource.Value.OpenConnectionAsync();
        await using var command = connection.CreateCommand();

        command.CommandText = "UPDATE side_effects_processing SET processing_started_at = now() WHERE id = ANY(@ids)";
        command.Parameters.AddWithValue("ids", ids.ToArray());
        await command.ExecuteNonQueryAsync();
    }
}
