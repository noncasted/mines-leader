using Shared;

namespace Game.MatchMetrics;

internal readonly record struct MatchMetricSlot(int SeriesId, SharedMatchMetrics.MatchSeriesKind Kind);
