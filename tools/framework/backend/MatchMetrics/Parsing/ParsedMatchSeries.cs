using Shared;

namespace Game.MatchMetrics;

internal sealed record ParsedMatchSeries(int Slot, MatchSeriesKey Key, SharedMatchMetrics.MatchSeriesKind Kind);
