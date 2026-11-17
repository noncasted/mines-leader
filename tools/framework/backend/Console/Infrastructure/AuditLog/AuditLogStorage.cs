using Common.Extensions;

namespace Console;

// A failed write or read throws: the page that called it shows the error, an action is never reported as audited when it is not.
public class AuditLogStorage : IAuditLogStorage
{
    public AuditLogStorage(IDbSource dbSource)
    {
        _dbSource = dbSource;
    }

    private readonly IDbSource _dbSource;

    public async Task Write(string action, string details)
    {
        await using var connection = await _dbSource.Value.OpenConnectionAsync();
        await using var cmd = connection.CreateCommand();

        cmd.CommandText = $@"
            INSERT INTO {AuditLogTable.Name} (id, timestamp, action, details)
            VALUES (@id, now(), @action, @details)
        ";
        cmd.Parameters.AddWithValue("id", Guid.NewGuid());
        cmd.Parameters.AddWithValue("action", action);
        cmd.Parameters.AddWithValue("details", details);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<AuditLogResult> GetPage(int offset, int limit)
    {
        await using var connection = await _dbSource.Value.OpenConnectionAsync();

        await using var countCmd = connection.CreateCommand();
        countCmd.CommandText = $"SELECT COUNT(*)::int FROM {AuditLogTable.Name}";
        var totalCount = (int)(await countCmd.ExecuteScalarAsync())!;

        await using var cmd = connection.CreateCommand();

        cmd.CommandText = $@"
            SELECT id, timestamp, action, details FROM {AuditLogTable.Name}
            ORDER BY timestamp DESC
            OFFSET @offset LIMIT @limit
        ";
        cmd.Parameters.AddWithValue("offset", offset);
        cmd.Parameters.AddWithValue("limit", limit);

        var entries = new List<AuditEntry>();
        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            entries.Add(new AuditEntry
            {
                Id = reader.GetGuid(0),
                Timestamp = reader.GetDateTime(1),
                Action = reader.GetString(2),
                Details = reader.GetString(3)
            });
        }

        return new AuditLogResult
        {
            Entries = entries,
            TotalCount = totalCount
        };
    }
}
