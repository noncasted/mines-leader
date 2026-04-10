using Common.Extensions;
using Infrastructure;
using Infrastructure.State;
using Microsoft.Extensions.Logging;
using Shared;

namespace Meta.Matches;

public class CardAnalyticsEntry
{
    public required string CardType { get; init; }
    public required int TimesInDeck { get; init; }
    public required int Wins { get; init; }
    public required int Losses { get; init; }
    public double WinRate => TimesInDeck > 0 ? (double)Wins / TimesInDeck * 100 : 0;
}

public interface ICardAnalyticsStorage
{
    Task<IReadOnlyList<CardAnalyticsEntry>> GetAnalytics();
}

public class CardAnalyticsStorage : ICardAnalyticsStorage
{
    public CardAnalyticsStorage(IDbSource dbSource, IStateSerializer serializer, ILogger<CardAnalyticsStorage> logger)
    {
        _dbSource = dbSource;
        _serializer = serializer;
        _logger = logger;
    }

    private readonly IDbSource _dbSource;
    private readonly IStateSerializer _serializer;
    private readonly ILogger<CardAnalyticsStorage> _logger;

    public async Task<IReadOnlyList<CardAnalyticsEntry>> GetAnalytics()
    {
        try
        {
            await using var connection = await _dbSource.Value.OpenConnectionAsync();
            await using var cmd = connection.CreateCommand();

            cmd.CommandText = "SELECT key, data::text FROM state_match_entity WHERE data IS NOT NULL";

            var cardStats = new Dictionary<CardType, (int InDeck, int Wins, int Losses)>();

            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var json = reader.GetString(1);

                try
                {
                    var state = _serializer.Deserialize<MatchState>(json);

                    if (state.Winner == Guid.Empty)
                        continue;

                    foreach (var (userId, cards) in state.ParticipantDecks)
                    {
                        var isWinner = userId == state.Winner;

                        foreach (var card in cards.Distinct())
                        {
                            if (!cardStats.TryGetValue(card, out var stats))
                                stats = (0, 0, 0);

                            cardStats[card] = (
                                stats.InDeck + 1,
                                stats.Wins + (isWinner ? 1 : 0),
                                stats.Losses + (isWinner ? 0 : 1)
                            );
                        }
                    }
                }
                catch (Exception e)
                {
                    _logger.LogWarning(e, "[CardAnalyticsStorage] Failed to deserialize match entry, skipping");
                }
            }

            return cardStats
                   .Select(kv => new CardAnalyticsEntry
                   {
                       CardType = kv.Key.ToString(),
                       TimesInDeck = kv.Value.InDeck,
                       Wins = kv.Value.Wins,
                       Losses = kv.Value.Losses
                   })
                   .OrderByDescending(e => e.TimesInDeck)
                   .ToList();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[CardAnalyticsStorage] Failed to get analytics");
            return Array.Empty<CardAnalyticsEntry>();
        }
    }
}
