using Shared;

namespace Game.MatchMetrics;

// Sum, Count, Max over the totals of the streams of the group (NaN of a gauge not counted); Streams - how many streams.
public sealed record MatchMetricGroupTotal(
    string Name,
    IReadOnlyDictionary<string, string> Group,
    SharedMatchMetrics.MatchSeriesKind Kind,
    double Sum,
    long Count,
    float Max,
    int Streams);
