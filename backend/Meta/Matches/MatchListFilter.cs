using Shared;

namespace Meta.Matches;

// Goes over the records in memory (IMatchSummaryCollection), newest by the start first: the list of the console and of
// the read API of MetaGateway (MatchHistoryEndpoints). All the given conditions together.
public sealed record MatchListFilter
{
    public const int DefaultLimit = 50;

    // A participant with this meta id, or (when PlayerId is null) a participant whose name at the setup is Nickname,
    // case ignored.
    public Guid? PlayerId { get; init; }
    public string? Nickname { get; init; }

    public GameMatchType? Type { get; init; }

    // True - a match closed with a winner, false - closed without one; a running match has no winner either.
    public bool? HasWinner { get; init; }

    // The start of a match: From inclusive, To exclusive.
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }

    public bool? IsClosed { get; init; }

    public int Offset { get; init; }
    public int Limit { get; init; } = DefaultLimit;

    // A meta id (a guid) - the matches of this user; any other text - the participants with this name at the setup.
    public MatchListFilter WithPlayer(string player)
    {
        return Guid.TryParse(player, out var id) == true
            ? this with { PlayerId = id, Nickname = null }
            : this with { PlayerId = null, Nickname = player };
    }

    public IReadOnlyList<KeyValuePair<Guid, MatchState>> Apply(IEnumerable<KeyValuePair<Guid, MatchState>> matches)
    {
        return matches.Where(entry => Matches(entry.Value))
                      .OrderByDescending(entry => entry.Value.StartDate)
                      .Skip(Offset)
                      .Take(Limit)
                      .ToArray();
    }

    public int Count(IEnumerable<MatchState> matches)
    {
        return matches.Count(Matches);
    }

    public bool Matches(MatchState match)
    {
        if (match.IsSetUp == false)
            return false;

        if (Type != null && match.Type != Type)
            return false;

        if (HasWinner != null && match.HasWinner != HasWinner)
            return false;

        if (From != null && match.StartDate < From)
            return false;

        if (To != null && match.StartDate >= To)
            return false;

        if (IsClosed != null && match.IsClosed != IsClosed)
            return false;

        return MatchesPlayer(match);
    }

    private bool MatchesPlayer(MatchState match)
    {
        if (PlayerId != null)
            return match.IsPlayer(PlayerId.Value);

        if (Nickname != null)
            return match.Nicknames.Values.Any(name => string.Equals(name, Nickname, StringComparison.OrdinalIgnoreCase));

        return true;
    }
}
