using Meta.Matches;
using Shared;

namespace MetaGateway.Matches;

// The parameters of /matches (CONTRACT.md). The binder answers 400 itself for a value it cannot read.
public sealed class MatchHistoryListQuery
{
    public string? Player { get; init; }
    public GameMatchType? Type { get; init; }
    public bool? Winner { get; init; }
    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }
    public bool? Closed { get; init; }
    public int? Offset { get; init; }
    public int? Limit { get; init; }

    // Null and the filter, or the reason of a 400.
    public string? FindError(int maxLimit, out MatchListFilter filter)
    {
        filter = new MatchListFilter();

        if (Offset < 0)
            return "offset from 0";

        if (Limit < 1 || Limit > maxLimit)
            return $"limit from 1 to {maxLimit}";

        filter = new MatchListFilter
        {
            Type = Type,
            HasWinner = Winner,
            From = From?.UtcDateTime,
            To = To?.UtcDateTime,
            IsClosed = Closed,
            Offset = Offset ?? 0,
            Limit = Limit ?? MatchListFilter.DefaultLimit
        };

        if (string.IsNullOrEmpty(Player) == false)
            filter = filter.WithPlayer(Player);

        return null;
    }
}
