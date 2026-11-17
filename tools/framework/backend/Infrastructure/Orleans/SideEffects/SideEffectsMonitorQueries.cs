using System.Text.Json;
using Common;
using Common.Extensions;

namespace Infrastructure;

// What the console sees and does: the counts of the tables and the entries of the retry queue.
internal class SideEffectsMonitorQueries
{
    public SideEffectsMonitorQueries(IDbSource dbSource)
    {
        _dbSource = dbSource;
    }

    private readonly IDbSource _dbSource;

    public async Task<SideEffectsStats> GetStats()
    {
        await using var connection = await _dbSource.Value.OpenConnectionAsync();
        await using var command = connection.CreateCommand();

        command.CommandText = $@"
            SELECT
                (SELECT COUNT(*)::int FROM side_effects_queue) AS queue_count,
                (SELECT COUNT(*)::int FROM side_effects_processing) AS processing_count,
                (SELECT COUNT(*)::int FROM side_effects_retry_queue) AS retry_count,
                (SELECT COUNT(*)::int FROM {SideEffectTables.DeadLetter}) AS dead_letter_count
        ";

        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();

        return new SideEffectsStats
        {
            QueueCount = reader.GetInt32(0),
            ProcessingCount = reader.GetInt32(1),
            RetryCount = reader.GetInt32(2),
            DeadLetterCount = reader.GetInt32(3)
        };
    }

    public async Task<IReadOnlyList<RetryQueueEntry>> GetRetryEntries(int limit)
    {
        await using var connection = await _dbSource.Value.OpenConnectionAsync();
        await using var command = connection.CreateCommand();

        command.CommandText = @"
            SELECT id, payload::text, retry_count, retry_after, created_at
            FROM side_effects_retry_queue
            ORDER BY retry_after
            LIMIT @limit
        ";
        command.Parameters.AddWithValue("limit", limit);

        var entries = new List<RetryQueueEntry>();
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var payloadJson = reader.GetString(1);
            var typeName = ExtractTypeName(payloadJson);

            entries.Add(new RetryQueueEntry
            {
                Id = reader.GetGuid(0),
                TypeName = typeName,
                RetryCount = reader.GetInt32(2),
                RetryAfter = reader.GetDateTime(3),
                CreatedAt = reader.GetDateTime(4)
            });
        }

        return entries;
    }

    public async Task DropRetryEntry(Guid id)
    {
        await using var connection = await _dbSource.Value.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM side_effects_retry_queue WHERE id = @id";
        command.Parameters.AddWithValue("id", id);
        await command.ExecuteNonQueryAsync();
    }

    public async Task RequeueRetryEntry(Guid id)
    {
        await using var connection = await _dbSource.Value.OpenConnectionAsync();
        await using var tx = await connection.BeginTransactionAsync();

        await using var insertCommand = connection.CreateCommand();
        insertCommand.Transaction = tx;

        insertCommand.CommandText = @"
            INSERT INTO side_effects_queue (id, payload, retry_count, created_at)
            SELECT id, payload, retry_count, created_at
            FROM side_effects_retry_queue
            WHERE id = @id
            ON CONFLICT (id) DO NOTHING
        ";
        insertCommand.Parameters.AddWithValue("id", id);
        await insertCommand.ExecuteNonQueryAsync();

        await using var deleteCommand = connection.CreateCommand();
        deleteCommand.Transaction = tx;
        deleteCommand.CommandText = "DELETE FROM side_effects_retry_queue WHERE id = @id";
        deleteCommand.Parameters.AddWithValue("id", id);
        await deleteCommand.ExecuteNonQueryAsync();

        await tx.CommitAsync();
    }

    // The payload is jsonb, it always parses. A row without a type name is shown as such: the screen is there to look at
    // the rows that went wrong, whatever wrote them.
    private static string ExtractTypeName(string payloadJson)
    {
        using var payload = JsonDocument.Parse(payloadJson);

        if (payload.RootElement.ValueKind != JsonValueKind.Object
            || payload.RootElement.TryGetProperty("$type", out var type) == false
            || type.ValueKind != JsonValueKind.String)
        {
            return "Unknown";
        }

        // "Namespace.Outer+Name`1[[Argument, Assembly]], Assembly": neither the assembly nor the generic arguments.
        var typeName = type.GetString()!;
        var fullNameEnd = typeName.IndexOfAny([',', '[']);
        var fullName = fullNameEnd < 0 ? typeName : typeName[..fullNameEnd];

        return fullName[(fullName.LastIndexOf('.') + 1)..];
    }
}
