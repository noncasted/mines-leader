using Shared;

namespace Game.MatchMetrics;

// The points of the chunks of a match by groups and steps, in memory (a match is under a megabyte). A counter is the sum of
// its buckets in a step. A gauge is the last value of a series in a step (NaN - unchanged), carried to the next steps while
// the group has points; the series of a group are summed.
internal static class MatchMetricPointsAggregation
{
    public static IReadOnlyList<MatchMetricGroupPoints> Of(IReadOnlyList<MatchMetricPointRow> rows, MatchMetricPointsQuery query)
    {
        var step = (long)query.Step;
        var groups = new List<MatchMetricGroupPoints>();

        foreach (var group in rows.GroupBy(row => (row.Name, Key: GroupKey(row.Group))))
        {
            var kinds = group.Select(row => row.Kind).Distinct().ToArray();

            if (kinds.Length > 1)
                throw new MatchMetricsQueryException($"The group {group.Key.Name} {group.Key.Key} has counters and gauges: group by more tags");

            var kind = kinds[0];
            var series = group.GroupBy(row => (row.SeriesId, row.StreamId)).Select(one => Buckets(one, query.Axis, step)).ToList();
            var points = kind == SharedMatchMetrics.MatchSeriesKind.Counter ? Sum(series) : SumLast(series);
            var window = points.Where(point => (query.From == null || point.Time >= query.From) && (query.To == null || point.Time <= query.To));

            groups.Add(new MatchMetricGroupPoints(group.Key.Name, group.First().Group, kind, window.ToArray()));
        }

        return groups.OrderBy(group => group.Name, StringComparer.Ordinal).ThenBy(group => GroupKey(group.Group), StringComparer.Ordinal).ToArray();
    }

    // Per step of one series: the sum and the last value with the second it was taken at.
    private static SortedDictionary<long, (double Sum, long Second, double Last)> Buckets(
        IEnumerable<MatchMetricPointRow> rows,
        MatchMetricAxis axis,
        long step)
    {
        var buckets = new SortedDictionary<long, (double Sum, long Second, double Last)>();

        foreach (var row in rows)
        {
            var first = axis == MatchMetricAxis.Run ? row.Start : new DateTimeOffset(row.At, TimeSpan.Zero).ToUnixTimeSeconds();

            for (var i = 0; i < row.Values.Length; i++)
            {
                var value = row.Values[i];

                if (float.IsNaN(value))
                    continue;

                var second = first + i;
                var time = second - (second % step + step) % step;
                var known = buckets.TryGetValue(time, out var bucket) ? bucket : (0d, long.MinValue, double.NaN);
                var isLater = second >= known.Item2;

                buckets[time] = (known.Item1 + value, isLater ? second : known.Item2, isLater ? value : known.Item3);
            }
        }

        return buckets;
    }

    private static IEnumerable<MatchMetricPoint> Sum(List<SortedDictionary<long, (double Sum, long Second, double Last)>> series)
    {
        return series.SelectMany(buckets => buckets)
                     .GroupBy(bucket => bucket.Key)
                     .OrderBy(bucket => bucket.Key)
                     .Select(bucket => new MatchMetricPoint(bucket.Key, bucket.Sum(one => one.Value.Sum)));
    }

    private static IEnumerable<MatchMetricPoint> SumLast(List<SortedDictionary<long, (double Sum, long Second, double Last)>> series)
    {
        var times = series.SelectMany(buckets => buckets.Keys).Distinct().Order().ToArray();
        var last = new double?[series.Count];

        foreach (var time in times)
        {
            for (var i = 0; i < series.Count; i++)
            {
                if (series[i].TryGetValue(time, out var bucket))
                    last[i] = bucket.Last;
            }

            yield return new MatchMetricPoint(time, last.Sum(value => value ?? 0));
        }
    }

    private static string GroupKey(IReadOnlyDictionary<string, string> group)
    {
        return string.Join(',', group.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"{pair.Key}={pair.Value}"));
    }
}
