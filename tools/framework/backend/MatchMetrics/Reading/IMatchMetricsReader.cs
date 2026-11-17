namespace Game.MatchMetrics;

// The reading of the stored streams, for the console, the agents and the history of matches of a game (in the process of the
// host, AddMatchMetricsReader) and for the endpoints (MapMatchMetricsRead). A bad query (a name or a key out of the rule of
// names, a group of two kinds) throws MatchMetricsQueryException.
public interface IMatchMetricsReader
{
    // The streams of a match: the clients of the players and the server.
    Task<IReadOnlyList<MatchMetricStreamInfo>> GetStreams(Guid matchId);

    // The dictionary of the series of the game, all or of one name.
    Task<IReadOnlyList<MatchMetricSeriesInfo>> GetSeries(string? name);

    // The points of a match by groups: a counter summed, a gauge the last value of a step (summed over the series of a group).
    Task<IReadOnlyList<MatchMetricGroupPoints>> GetPoints(Guid matchId, MatchMetricPointsQuery query);

    // The totals by groups: of a match or across matches (the streams opened within Since .. Until).
    Task<IReadOnlyList<MatchMetricGroupTotal>> GetTotals(MatchMetricTotalsQuery query);

    // The events of a match ordered by the time of the run.
    Task<IReadOnlyList<MatchMetricEventInfo>> GetEvents(Guid matchId, MatchMetricEventsQuery query);
}
