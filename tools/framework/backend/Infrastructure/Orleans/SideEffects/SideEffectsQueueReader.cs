using Common.Extensions;
using Infrastructure.State;
using Microsoft.Extensions.Logging;

namespace Infrastructure;

// Takes entries of side_effects_queue into side_effects_processing for a worker.
internal class SideEffectsQueueReader
{
    public SideEffectsQueueReader(IDbSource dbSource, IStateSerializer serializer, ILogger logger)
    {
        _dbSource = dbSource;
        _serializer = serializer;
        _logger = logger;
    }

    private readonly IDbSource _dbSource;
    private readonly IStateSerializer _serializer;
    private readonly ILogger _logger;

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
                // The entry is in processing already: it fails through the worker like any other effect, with the
                // error of the deserialization in its retry and dead letter records.
                _logger.LogError(e, "[SideEffectsStorage] Failed to deserialize side effect {Id}", id);

                entries.Add(new SideEffectEntry
                {
                    Id = id,
                    Effect = new DeadLetterSideEffect(e),
                    RetryCount = retryCount
                });
            }
        }

        return entries;
    }
}
