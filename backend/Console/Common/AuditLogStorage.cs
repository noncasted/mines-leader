using Common.Extensions;
using Infrastructure;
using Microsoft.Extensions.Logging;

namespace Console;

public class AuditEntry
{
    public Guid Id { get; init; }
    public DateTime Timestamp { get; init; }
    public required string Action { get; init; }
    public required string Details { get; init; }
}

public class AuditLogResult
{
    public required IReadOnlyList<AuditEntry> Entries { get; init; }
    public required int TotalCount { get; init; }
}

public interface IAuditLogStorage
{
    Task Write(string action, string details);
    Task<AuditLogResult> GetPage(int offset, int limit);
    Task EnsureTable();
}

public class AuditLogStorage : IAuditLogStorage
{
    public AuditLogStorage(IDbSource dbSource, ILogger<AuditLogStorage> logger)
    {
        _dbSource = dbSource;
        _logger = logger;
    }

    private const string TableName = "audit_log";

    private readonly IDbSource _dbSource;
    private readonly ILogger<AuditLogStorage> _logger;

    public async Task EnsureTable()
    {
        try
        {
            await using var connection = await _dbSource.Value.OpenConnectionAsync();
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = $@"
                CREATE TABLE IF NOT EXISTS {TableName} (
                    id uuid NOT NULL DEFAULT gen_random_uuid(),
                    timestamp timestamptz NOT NULL DEFAULT now(),
                    action text NOT NULL,
                    details text NOT NULL DEFAULT '',
                    PRIMARY KEY (id)
                );
                CREATE INDEX IF NOT EXISTS ix_{TableName}_timestamp ON {TableName} USING btree (timestamp DESC);
            ";
            await cmd.ExecuteNonQueryAsync();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[AuditLog] Failed to ensure table");
        }
    }

    public async Task Write(string action, string details)
    {
        try
        {
            await using var connection = await _dbSource.Value.OpenConnectionAsync();
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = $@"
                INSERT INTO {TableName} (id, timestamp, action, details)
                VALUES (@id, now(), @action, @details)
            ";
            cmd.Parameters.AddWithValue("id", Guid.NewGuid());
            cmd.Parameters.AddWithValue("action", action);
            cmd.Parameters.AddWithValue("details", details);
            await cmd.ExecuteNonQueryAsync();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[AuditLog] Failed to write entry");
        }
    }

    public async Task<AuditLogResult> GetPage(int offset, int limit)
    {
        try
        {
            await using var connection = await _dbSource.Value.OpenConnectionAsync();

            await using var countCmd = connection.CreateCommand();
            countCmd.CommandText = $"SELECT COUNT(*)::int FROM {TableName}";
            var totalCount = (int)(await countCmd.ExecuteScalarAsync())!;

            await using var cmd = connection.CreateCommand();
            cmd.CommandText = $@"
                SELECT id, timestamp, action, details FROM {TableName}
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
        catch (Exception e)
        {
            _logger.LogError(e, "[AuditLog] Failed to read page (offset={Offset}, limit={Limit})", offset, limit);
            return new AuditLogResult { Entries = Array.Empty<AuditEntry>(), TotalCount = 0 };
        }
    }
}
