using Common.Extensions;
using Microsoft.Extensions.Logging;

namespace Infrastructure;

// The moves back into side_effects_queue a worker runs by itself: stuck entries and the retries that are due.
internal class SideEffectsRequeue
{
    public SideEffectsRequeue(IDbSource dbSource, ILogger logger)
    {
        _dbSource = dbSource;
        _logger = logger;
    }

    private readonly IDbSource _dbSource;
    private readonly ILogger _logger;

    public async Task RequeueStuckOlderThan(TimeSpan age)
    {
        await using var connection = await _dbSource.Value.OpenConnectionAsync();
        await using var command = connection.CreateCommand();

        command.CommandText = @"
            WITH stuck AS (
                DELETE FROM side_effects_processing
                WHERE processing_started_at < @cutoff
                RETURNING id, payload, retry_count, created_at
            )
            INSERT INTO side_effects_queue (id, payload, retry_count, created_at)
            SELECT id, payload, retry_count, created_at FROM stuck
            ON CONFLICT (id) DO NOTHING
        ";
        command.Parameters.AddWithValue("cutoff", DateTime.UtcNow - age);

        var moved = await command.ExecuteNonQueryAsync();

        if (moved > 0)
            _logger.LogWarning("[SideEffectsStorage] Requeued {Count} stuck entries older than {Age}", moved, age);
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
