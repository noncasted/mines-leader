using Shared;

namespace Game.MatchMetrics;

public sealed record MatchMetricSeriesInfo(int Id, string Name, SharedMatchMetrics.MatchSeriesKind Kind, IReadOnlyDictionary<string, string> Tags);
