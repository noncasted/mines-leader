namespace Game.MatchMetrics;

// From / To - inclusive, in the unit of the axis (seconds of the run or unix seconds), on the start of a step.
public sealed record MatchMetricPointsQuery
{
    public MatchMetricFilter Filter { get; init; } = new();
    public MatchMetricStep Step { get; init; } = MatchMetricStep.Second;
    public MatchMetricAxis Axis { get; init; } = MatchMetricAxis.Run;
    public long? From { get; init; }
    public long? To { get; init; }
}
