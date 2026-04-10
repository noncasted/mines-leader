using Common.Extensions;
using Infrastructure;
using Infrastructure.State;
using Microsoft.Extensions.Logging;

namespace Meta.Matches;

public class MatchHistoryEntry
{
    public required Guid Id { get; init; }
    public required DateTime Date { get; init; }
    public required string Type { get; init; }
    public required Guid Winner { get; init; }
    public required TimeSpan Duration { get; init; }
    public required int ParticipantCount { get; init; }
}

public class MatchHistoryResult
{
    public required IReadOnlyList<MatchHistoryEntry> Entries { get; init; }
    public required int TotalCount { get; init; }
}

public interface IMatchHistoryStorage
{
    Task<MatchHistoryResult> GetPage(int offset, int limit, string? typeFilter = null);
}

public class MatchHistoryStorage : IMatchHistoryStorage
{
    public MatchHistoryStorage(IDbSource dbSource, IStateSerializer serializer, ILogger<MatchHistoryStorage> logger)
    {
        _dbSource = dbSource;
        _serializer = serializer;
        _logger = logger;
    }

    private readonly IDbSource _dbSource;
    private readonly IStateSerializer _serializer;
    private readonly ILogger<MatchHistoryStorage> _logger;

    public async Task<MatchHistoryResult> GetPage(int offset, int limit, string? typeFilter = null)
    {
        try
        {
            await using var connection = await _dbSource.Value.OpenConnectionAsync();

            await using var countCmd = connection.CreateCommand();
            countCmd.CommandText = "SELECT COUNT(*)::int FROM state_match_entity WHERE data IS NOT NULL";
            var totalCount = (int)(await countCmd.ExecuteScalarAsync())!;

            await using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT key, data::text FROM state_match_entity
                WHERE data IS NOT NULL
                ORDER BY key DESC
                OFFSET @offset LIMIT @limit
            ";
            cmd.Parameters.AddWithValue("offset", offset);
            cmd.Parameters.AddWithValue("limit", limit);

            var entries = new List<MatchHistoryEntry>();
            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var id = reader.GetGuid(0);
                var json = reader.GetString(1);

                try
                {
                    var state = _serializer.Deserialize<MatchState>(json);

                    if (typeFilter != null && state.Type.ToString() != typeFilter)
                        continue;

                    entries.Add(new MatchHistoryEntry
                    {
                        Id = id,
                        Date = state.StartDate,
                        Type = state.Type.ToString(),
                        Winner = state.Winner,
                        Duration = state.Time,
                        ParticipantCount = state.Participants.Count
                    });
                }
                catch (Exception e)
                {
                    _logger.LogWarning(e, "[MatchHistoryStorage] Failed to deserialize match {Id}, skipping", id);
                }
            }

            return new MatchHistoryResult
            {
                Entries = entries,
                TotalCount = totalCount
            };
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[MatchHistoryStorage] Failed to get page (offset={Offset}, limit={Limit})", offset, limit);
            return new MatchHistoryResult { Entries = Array.Empty<MatchHistoryEntry>(), TotalCount = 0 };
        }
    }
}
