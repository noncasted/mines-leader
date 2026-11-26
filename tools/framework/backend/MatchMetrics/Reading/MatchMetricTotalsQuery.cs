namespace Game.MatchMetrics;

// MatchId - the totals of one match; without it across matches, the streams opened within Since .. Until.
public sealed record MatchMetricTotalsQuery
{
    public Guid? MatchId { get; init; }
    public MatchMetricFilter Filter { get; init; } = new();
    public DateTime? Since { get; init; }
    public DateTime? Until { get; init; }
}
