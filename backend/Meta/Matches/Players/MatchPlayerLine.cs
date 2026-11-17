using Game.MatchMetrics;

namespace Meta.Matches;

// One counter of a player by steps: Key is the name of the series, with the tag value after a colon for a tagged one
// (cells.opened:click, cards.played:trinity).
public sealed record MatchPlayerLine(string Key, IReadOnlyList<MatchMetricPoint> Points);
