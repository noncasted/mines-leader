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
    public MatchHistoryStorage(IStateStorageReader storageReader, ILogger<MatchHistoryStorage> logger)
    {
        _storageReader = storageReader;
        _logger = logger;
    }

    private readonly IStateStorageReader _storageReader;
    private readonly ILogger<MatchHistoryStorage> _logger;

    public async Task<MatchHistoryResult> GetPage(int offset, int limit, string? typeFilter = null)
    {
        var page = await _storageReader.ReadPage<Guid, MatchState>(offset, limit);

        var entries = new List<MatchHistoryEntry>(page.Entries.Count);

        foreach (var (id, state) in page.Entries)
        {
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

        return new MatchHistoryResult
        {
            Entries = entries,
            TotalCount = page.TotalCount
        };
    }
}
