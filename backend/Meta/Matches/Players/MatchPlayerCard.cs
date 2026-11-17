namespace Meta.Matches;

// One participant of a match: its record and what its client wrote to the collector of the game metrics. Streams 0 - no
// metrics of this player (a bot, an old client); then the totals are 0 and there are no events.
public sealed record MatchPlayerCard
{
    public required Guid UserId { get; init; }

    // The name at the setup of the match.
    public required string Nickname { get; init; }

    public required bool IsWinner { get; init; }
    public required int RatingChange { get; init; }

    // The streams of the client of this player in the match: a new one after a reconnect.
    public required int Streams { get; init; }

    public required double CellsOpened { get; init; }

    // By the tag source: click / cascade / card ("" - a series without it).
    public required IReadOnlyDictionary<string, double> CellsBySource { get; init; }

    // cascades, flags.set, flags.removed, mines.hit, turns.
    public required IReadOnlyDictionary<string, double> Counters { get; init; }

    public required double CardsPlayed { get; init; }

    // By the tag card: CardType in lower case.
    public required IReadOnlyDictionary<string, double> CardsByType { get; init; }

    // turn.end, card.play, match.end by the time of the match clock.
    public required IReadOnlyList<MatchPlayerMoment> Events { get; init; }
}
