using System.Text.Json;
using Game.MatchMetrics;
using Shared;

namespace Tests.Meta.Matches;

// The collector of the game metrics of one match, filled by a test: the series of the players with their tags, grouped
// on reading the way the collector does (a group is one name, "" for a tag the series has not).
public sealed class MatchMetricsReaderFake : IMatchMetricsReader
{
    private readonly List<(Guid User, string Name, IReadOnlyDictionary<string, string> Tags, IReadOnlyList<MatchMetricPoint> Points)> _series = new();
    private readonly List<MatchMetricEventInfo> _events = new();
    private readonly List<MatchMetricStreamInfo> _streams = new();

    public void AddStream(Guid matchId, Guid userId)
    {
        _streams.Add(new MatchMetricStreamInfo(Guid.NewGuid(), matchId, userId, Guid.NewGuid(),
            SharedMatchMetrics.MatchStreamKind.Client, DateTime.UtcNow, 1, DateTime.UtcNow));
    }

    // A counter of a player: points by the second of the match clock.
    public void AddCounter(Guid userId, string name, IReadOnlyDictionary<string, string> tags, params (long Time, double Value)[] points)
    {
        _series.Add((userId, name, tags, points.Select(point => new MatchMetricPoint(point.Time, point.Value)).ToArray()));
    }

    public void AddEvent(Guid userId, string name, float time, object fields)
    {
        _events.Add(new MatchMetricEventInfo(Guid.NewGuid(), userId, SharedMatchMetrics.MatchStreamKind.Client, 1,
            _events.Count, time, DateTime.UtcNow, name, JsonSerializer.SerializeToElement(fields)));
    }

    public Task<IReadOnlyList<MatchMetricStreamInfo>> GetStreams(Guid matchId)
    {
        return Task.FromResult<IReadOnlyList<MatchMetricStreamInfo>>(_streams.Where(stream => stream.MatchId == matchId).ToArray());
    }

    public Task<IReadOnlyList<MatchMetricSeriesInfo>> GetSeries(string? name)
    {
        throw new NotSupportedException();
    }

    public Task<IReadOnlyList<MatchMetricGroupPoints>> GetPoints(Guid matchId, MatchMetricPointsQuery query)
    {
        var groups = Select(query.Filter)
                     .GroupBy(series => GroupKey(series.User, series.Tags, query.Filter.GroupBy))
                     .Select(group => new MatchMetricGroupPoints(query.Filter.Name!, Group(group.First(), query.Filter.GroupBy),
                         SharedMatchMetrics.MatchSeriesKind.Counter,
                         group.SelectMany(series => series.Points)
                              .GroupBy(point => point.Time / (int)query.Step * (int)query.Step)
                              .OrderBy(step => step.Key)
                              .Select(step => new MatchMetricPoint(step.Key, step.Sum(point => point.Value)))
                              .ToArray()))
                     .ToArray();

        return Task.FromResult<IReadOnlyList<MatchMetricGroupPoints>>(groups);
    }

    public Task<IReadOnlyList<MatchMetricGroupTotal>> GetTotals(MatchMetricTotalsQuery query)
    {
        var totals = Select(query.Filter)
                     .GroupBy(series => GroupKey(series.User, series.Tags, query.Filter.GroupBy))
                     .Select(group => new MatchMetricGroupTotal(query.Filter.Name!, Group(group.First(), query.Filter.GroupBy),
                         SharedMatchMetrics.MatchSeriesKind.Counter,
                         group.SelectMany(series => series.Points).Sum(point => point.Value),
                         group.SelectMany(series => series.Points).Count(),
                         (float)group.SelectMany(series => series.Points).Max(point => point.Value),
                         1))
                     .ToArray();

        return Task.FromResult<IReadOnlyList<MatchMetricGroupTotal>>(totals);
    }

    public Task<IReadOnlyList<MatchMetricEventInfo>> GetEvents(Guid matchId, MatchMetricEventsQuery query)
    {
        var events = _events.Where(info => query.Name == null || info.Name == query.Name)
                            .Where(info => query.UserId == null || info.UserId == query.UserId)
                            .OrderBy(info => info.Time)
                            .ToArray();

        return Task.FromResult<IReadOnlyList<MatchMetricEventInfo>>(events);
    }

    private IEnumerable<(Guid User, string Name, IReadOnlyDictionary<string, string> Tags, IReadOnlyList<MatchMetricPoint> Points)> Select(
        MatchMetricFilter filter)
    {
        return _series.Where(series => series.Name == filter.Name)
                      .Where(series => filter.UserId == null || series.User == filter.UserId);
    }

    private static string GroupKey(Guid user, IReadOnlyDictionary<string, string> tags, IReadOnlyList<string> groupBy)
    {
        return string.Join("|", groupBy.Select(key => Value(user, tags, key)));
    }

    private static IReadOnlyDictionary<string, string> Group(
        (Guid User, string Name, IReadOnlyDictionary<string, string> Tags, IReadOnlyList<MatchMetricPoint> Points) series,
        IReadOnlyList<string> groupBy)
    {
        return groupBy.ToDictionary(key => key, key => Value(series.User, series.Tags, key));
    }

    private static string Value(Guid user, IReadOnlyDictionary<string, string> tags, string key)
    {
        if (key == MatchMetricFilter.UserKey)
            return user.ToString();

        return tags.GetValueOrDefault(key, "");
    }
}
