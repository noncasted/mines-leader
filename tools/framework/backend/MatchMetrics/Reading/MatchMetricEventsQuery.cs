namespace Game.MatchMetrics;

// From / To - inclusive, seconds of the clock of the run.
public sealed record MatchMetricEventsQuery
{
    public string? Name { get; init; }
    public Guid? UserId { get; init; }
    public float? From { get; init; }
    public float? To { get; init; }
}
