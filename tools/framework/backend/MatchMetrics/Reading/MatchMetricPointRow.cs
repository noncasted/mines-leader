using Shared;

namespace Game.MatchMetrics;

// One chunk as the points read it: its series, its group and its buckets of a second from Start (run) and At (server).
internal readonly record struct MatchMetricPointRow(
    int SeriesId,
    Guid StreamId,
    string Name,
    SharedMatchMetrics.MatchSeriesKind Kind,
    IReadOnlyDictionary<string, string> Group,
    int Start,
    DateTime At,
    float[] Values);
