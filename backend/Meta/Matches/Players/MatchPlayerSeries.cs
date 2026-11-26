using Game.MatchMetrics;

namespace Meta.Matches;

// The counters of one player of a match by steps (summed in a step); Time of a point - the start of its step on the axis.
// A counter without points in the match has no line.
public sealed record MatchPlayerSeries
{
    public required Guid UserId { get; init; }
    public required int Step { get; init; }
    public required MatchMetricAxis Axis { get; init; }
    public required IReadOnlyList<MatchPlayerLine> Lines { get; init; }
}
