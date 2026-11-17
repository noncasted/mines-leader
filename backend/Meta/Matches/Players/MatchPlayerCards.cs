using Game.MatchMetrics;
using static Meta.Matches.MatchGameMetrics;
using Shared;

namespace Meta.Matches;

// The participants of a match for the read API and the console: the record (MatchState) joined with the totals and the
// events of the collector of the game metrics. A few queries a match, whatever the number of players.
public sealed class MatchPlayerCards
{
    public MatchPlayerCards(IMatchMetricsReader reader)
    {
        _reader = reader;
    }

    private readonly IMatchMetricsReader _reader;

    // Every participant, in the order of the record.
    public async Task<IReadOnlyList<MatchPlayerCard>> Get(Guid matchId, MatchState match)
    {
        var streams = await _reader.GetStreams(matchId);
        var events = await _reader.GetEvents(matchId, new MatchMetricEventsQuery());
        var cells = await Totals(matchId, CellsOpened, SourceTag);
        var cards = await Totals(matchId, CardsPlayed, CardTag);
        var counters = new Dictionary<string, IReadOnlyList<MatchMetricGroupTotal>>();

        foreach (var name in PlainCounters)
            counters[name] = await Totals(matchId, name);

        return match.Participants
                    .Select(userId => new MatchPlayerCard
                    {
                        UserId = userId,
                        Nickname = match.NicknameOf(userId),
                        IsWinner = match.Winner == userId,
                        RatingChange = match.RatingChanges.GetValueOrDefault(userId),
                        Streams = streams.Count(stream =>
                            stream.UserId == userId && stream.Kind == SharedMatchMetrics.MatchStreamKind.Client),
                        CellsOpened = Sum(cells, userId),
                        CellsBySource = By(cells, userId, SourceTag),
                        Counters = counters.ToDictionary(entry => entry.Key, entry => Sum(entry.Value, userId)),
                        CardsPlayed = Sum(cards, userId),
                        CardsByType = By(cards, userId, CardTag),
                        Events = events.Where(info => info.UserId == userId && PlayerEvents.Contains(info.Name))
                                       .Select(MatchPlayerMoment.From)
                                       .ToArray()
                    })
                    .ToArray();
    }

    // The totals of one series of the match by user and the given tags.
    private Task<IReadOnlyList<MatchMetricGroupTotal>> Totals(Guid matchId, string name, params string[] tags)
    {
        return _reader.GetTotals(new MatchMetricTotalsQuery
        {
            MatchId = matchId,
            Filter = new MatchMetricFilter { Name = name, GroupBy = tags.Prepend(MatchMetricFilter.UserKey).ToArray() }
        });
    }

    private static IEnumerable<MatchMetricGroupTotal> Of(IReadOnlyList<MatchMetricGroupTotal> totals, Guid userId)
    {
        return totals.Where(total => Guid.Parse(total.Group[MatchMetricFilter.UserKey]) == userId);
    }

    private static double Sum(IReadOnlyList<MatchMetricGroupTotal> totals, Guid userId)
    {
        return Of(totals, userId).Sum(total => total.Sum);
    }

    // A series without the tag is "" there.
    private static IReadOnlyDictionary<string, double> By(IReadOnlyList<MatchMetricGroupTotal> totals, Guid userId, string tag)
    {
        return Of(totals, userId).GroupBy(total => total.Group[tag])
                                 .OrderBy(group => group.Key, StringComparer.Ordinal)
                                 .ToDictionary(group => group.Key, group => group.Sum(total => total.Sum));
    }
}
