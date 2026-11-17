namespace Meta.Matches;

// The names of the series, tags and events the game writes to the collector of the game metrics (the contract of the
// game metrics: .claude/docs/TELEMETRY.md, "Match metrics"). A change there is a change here.
public static class MatchGameMetrics
{
    // The client of a player: counters.
    public const string CellsOpened = "cells.opened";
    public const string Cascades = "cascades";
    public const string FlagsSet = "flags.set";
    public const string FlagsRemoved = "flags.removed";
    public const string MinesHit = "mines.hit";
    public const string CardsPlayed = "cards.played";
    public const string Turns = "turns";

    // cells.opened: click / cascade / card; cards.played: the name of the CardType in lower case
    // (Trebuchet_Max -> trebuchet_max). Shown as it is, never parsed back.
    public const string SourceTag = "source";
    public const string CardTag = "card";

    // The client of a player: events.
    public const string TurnEnd = "turn.end";
    public const string CardPlay = "card.play";
    public const string MatchEnd = "match.end";

    // The server of the session (user Guid.Empty): a gauge and events.
    public const string SessionUsers = "session.users";
    public const string SessionJoin = "session.join";
    public const string SessionLeave = "session.leave";
    public const string SessionEnd = "session.end";

    // The counters of a player without a tag, in the order of the cards and of the csv.
    public static readonly IReadOnlyList<string> PlainCounters = [Cascades, FlagsSet, FlagsRemoved, MinesHit, Turns];

    // The events of a player shown with the player.
    public static readonly IReadOnlyList<string> PlayerEvents = [TurnEnd, CardPlay, MatchEnd];
}
