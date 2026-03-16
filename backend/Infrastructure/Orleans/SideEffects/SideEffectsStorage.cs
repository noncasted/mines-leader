using Common.Extensions;
using Infrastructure.State;
using Npgsql;
using NpgsqlTypes;
using Orleans.Serialization;

namespace Infrastructure;

public class SideEffectEntry
{
    public required Guid Id { get; init; }
    public required ISideEffect Effect { get; init; }
    public required int RetryCount { get; init; }
}

public interface ISideEffectsStorage
{
    Task Write(ISideEffect effects);

    // Write new side effects into side_effects_queue (called within Transactions.Process() pgTransaction)
    Task Write(NpgsqlTransaction transaction, IReadOnlyList<ISideEffect> effects);

    // Atomically move up to `count` oldest entries from queue → processing. Returns them.
    Task<IReadOnlyList<SideEffectEntry>> Read(int count);

    // Delete from side_effects_processing within an existing Postgres transaction (for ITransactionalSideEffect)
    Task CompleteProcessing(NpgsqlTransaction transaction, Guid id);

    // Delete from side_effects_processing standalone (for simple ISideEffect)
    Task CompleteProcessing(Guid id);

    // Move from side_effects_processing → side_effects_retry_queue (or delete if max retries exceeded)
    Task FailProcessing(Guid id, int retryCount, int maxRetryCount, float incrementalRetryDelaySeconds);

    // Move entries from side_effects_retry_queue → side_effects_queue where retry_after <= now
    Task RequeueReady();
}

public class SideEffectsStorage : ISideEffectsStorage
{
    public SideEffectsStorage(IDbSource dbSource, IStateSerializer serializer)
    {
        _dbSource = dbSource;
        _serializer = serializer;
    }

    private readonly IDbSource _dbSource;
    private readonly IStateSerializer _serializer;

    public async Task Write(ISideEffect effects)
    {
        await using var connection = await _dbSource.Value.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            await Write(transaction, [effects]);
            await transaction.CommitAsync();
        }
        catch (Exception e)
        {
            await transaction.RollbackAsync();
        }
    }

    public async Task Write(NpgsqlTransaction transaction, IReadOnlyList<ISideEffect> effects)
    {
        foreach (var effect in effects)
        {
            var payload = _serializer.Serialize(effect);

            await using var command = transaction.Connection!.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = @"
                INSERT INTO side_effects_queue (id, payload, retry_count, created_at)
                VALUES (@id, @payload::jsonb, 0, now())
            ";
            command.Parameters.AddWithValue("id", Guid.NewGuid());
            var payloadParam = command.Parameters.AddWithValue("payload", payload);
            payloadParam.NpgsqlDbType = NpgsqlDbType.Jsonb;

            await command.ExecuteNonQueryAsync();
        }
    }

    // Atomically move oldest `count` entries from queue to processing and return them.
    // Uses CTE to prevent concurrent workers from picking the same entries.
    public async Task<IReadOnlyList<SideEffectEntry>> Read(int count)
    {
        await using var connection = await _dbSource.Value.OpenConnectionAsync();
        await using var command = connection.CreateCommand();

        command.CommandText = @"
            WITH picked AS (
                SELECT id, payload, retry_count, created_at
                FROM side_effects_queue
                ORDER BY created_at
                LIMIT @count
                FOR UPDATE SKIP LOCKED
            ),
            inserted AS (
                INSERT INTO side_effects_processing (id, payload, retry_count, created_at, processing_started_at)
                SELECT id, payload, retry_count, created_at, now()
                FROM picked
            ),
            deleted AS (
                DELETE FROM side_effects_queue
                WHERE id IN (SELECT id FROM picked)
            )
            SELECT id, payload::text, retry_count FROM picked
        ";
        command.Parameters.AddWithValue("count", count);

        var entries = new List<SideEffectEntry>();

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var id = reader.GetGuid(0);
            var payloadJson = reader.GetString(1);
            var retryCount = reader.GetInt32(2);

            try
            {
                var effect = _serializer.Deserialize<ISideEffect>(payloadJson);
                entries.Add(new SideEffectEntry { Id = id, Effect = effect, RetryCount = retryCount });
            }
            catch (Exception e)
            {
                // If deserialization fails, the entry stays in processing and will be failed later.
                entries.Add(new SideEffectEntry
                    {
                        Id = id,
                        Effect = new DeadLetterSideEffect(),
                        RetryCount = retryCount
                    }
                );
            }
        }

        return entries;
    }

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

    public async Task FailProcessing(Guid id, int retryCount, int maxRetryCount, float incrementalRetryDelaySeconds)
    {
        if (retryCount >= maxRetryCount)
        {
            await using var connection = await _dbSource.Value.OpenConnectionAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM side_effects_processing WHERE id = @id";
            command.Parameters.AddWithValue("id", id);
            await command.ExecuteNonQueryAsync();
            return;
        }

        var delaySeconds = (retryCount + 1) * incrementalRetryDelaySeconds;
        var retryAfter = DateTime.UtcNow.AddSeconds(delaySeconds);
        var newRetryCount = retryCount + 1;

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

    public async Task RequeueReady()
    {
        await using var connection = await _dbSource.Value.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = @"
            WITH ready AS (
                DELETE FROM side_effects_retry_queue
                WHERE retry_after <= now()
                RETURNING id, payload, retry_count, created_at
            )
            INSERT INTO side_effects_queue (id, payload, retry_count, created_at)
            SELECT id, payload, retry_count, created_at FROM ready
            ON CONFLICT (id) DO NOTHING
        ";
        await command.ExecuteNonQueryAsync();
    }
}

// Sentinel used when payload deserialization fails — immediately fails without executing.
internal class DeadLetterSideEffect : ISideEffect
{
    public Task Execute(IOrleans orleans) =>
        Task.FromException(new InvalidOperationException("[SideEffects] Dead letter: failed to deserialize payload."));
}