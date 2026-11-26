using Common.Extensions;
using Infrastructure.State;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;

namespace Infrastructure;

// New entries of side_effects_queue.
internal class SideEffectsQueueWriter
{
    public SideEffectsQueueWriter(IDbSource dbSource, IStateSerializer serializer, ILogger logger)
    {
        _dbSource = dbSource;
        _serializer = serializer;
        _logger = logger;
    }

    private readonly IDbSource _dbSource;
    private readonly IStateSerializer _serializer;
    private readonly ILogger _logger;

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
            _logger.LogError(e, "[SideEffectsStorage] Failed to write side effect");

            try
            {
                await transaction.RollbackAsync();
            }
            catch (Exception rollbackException)
            {
                _logger.LogWarning(rollbackException, "[SideEffectsStorage] Rollback failed after side effect write error");
            }

            throw;
        }
    }

    public async Task Write(NpgsqlTransaction transaction, IReadOnlyList<ISideEffect> effects)
    {
        if (effects.Count == 0)
            return;

        await using var batch = new NpgsqlBatch(transaction.Connection!, transaction);
        batch.BatchCommands.Add(BuildWriteCommand(effects));
        await batch.ExecuteNonQueryAsync();
    }

    public NpgsqlBatchCommand BuildWriteCommand(IReadOnlyList<ISideEffect> effects)
    {
        var command = new NpgsqlBatchCommand();
        var values = new List<string>(effects.Count);

        for (var i = 0; i < effects.Count; i++)
        {
            var payload = _serializer.Serialize(effects[i]);

            var idParam = $"id{i}";
            var payloadParam = $"payload{i}";

            command.Parameters.AddWithValue(idParam, Guid.NewGuid());
            var p = command.Parameters.AddWithValue(payloadParam, payload);
            p.NpgsqlDbType = NpgsqlDbType.Jsonb;

            values.Add($"(@{idParam}, @{payloadParam}::jsonb, 0, now())");
        }

        command.CommandText = $@"
            INSERT INTO side_effects_queue (id, payload, retry_count, created_at)
            VALUES {string.Join(", ", values)}
        ";

        return command;
    }
}
