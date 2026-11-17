using Shared;

namespace Game.MatchMetrics;

// Group - the values of the keys of GroupBy ("user" - the meta id); a series without a tag of the group has "" there.
public sealed record MatchMetricGroupPoints(
    string Name,
    IReadOnlyDictionary<string, string> Group,
    SharedMatchMetrics.MatchSeriesKind Kind,
    IReadOnlyList<MatchMetricPoint> Points);
