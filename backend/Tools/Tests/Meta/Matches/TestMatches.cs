using Meta.Matches;
using Shared;

namespace Tests.Meta.Matches;

// Records of matches built by their events, as the grain applies them.
public static class TestMatches
{
    public static MatchState Create(
        DateTime start,
        IReadOnlyList<(Guid Id, string Name)> players,
        Guid? winner = null,
        TimeSpan? closedAfter = null,
        GameMatchType type = GameMatchType.LastManStandingTurnBased)
    {
        var match = new MatchState();
        var ids = players.Select(player => player.Id).ToList();

        match.Apply(new MatchSetup(type, start, ids,
            ids.ToDictionary(id => id, _ => (IReadOnlyList<CardType>)new List<CardType> { CardType.Trebuchet }),
            players.ToDictionary(player => player.Id, player => player.Name)));

        if (winner != null)
        {
            match.Apply(new MatchCompleted(winner.Value, closedAfter!.Value));
            match.Apply(new MatchRatingCalculated(ids.ToDictionary(id => id, id => id == winner ? 25 : -20)));
        }

        if (closedAfter != null)
            match.Apply(new MatchClosed(start + closedAfter.Value));

        return match;
    }
}
