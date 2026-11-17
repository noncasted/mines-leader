namespace Game.MatchMetrics;

// The resolution of the read points, in seconds: the buckets of a second are summed (a gauge - the last) on reading.
public enum MatchMetricStep
{
    Second = 1,
    TenSeconds = 10,
    Minute = 60
}
