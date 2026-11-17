using Shared;

namespace Game.MatchMetrics;

// Names and fields follow SharedMatchMetricNames: a wrong one is a mistake of the code and throws. Any thread.
public interface IMatchMetricsServerStream
{
    void Event(string name, params SharedMatchMetrics.MatchEventField[] fields);

    // A gauge without tags: the last value of its second.
    void Gauge(string name, float value);
}
