namespace Game.MatchMetrics;

// The totals of one batch per series, one row per series (an upsert may not touch a row twice). NaN (a gauge unchanged)
// is not a value; a series of NaN only has no row.
internal static class MatchMetricTotals
{
    public static List<MatchMetricTotal> Of(IReadOnlyList<ParsedMatchChunk> chunks, Func<int, int> seriesOf)
    {
        var totals = new Dictionary<int, (double Sum, int Count, float Max)>();

        foreach (var chunk in chunks)
        {
            var series = seriesOf(chunk.Slot);
            var total = totals.TryGetValue(series, out var known) ? known : (0d, 0, float.MinValue);

            foreach (var value in chunk.Values)
            {
                if (float.IsNaN(value))
                    continue;

                total = (total.Item1 + value, total.Item2 + 1, Math.Max(total.Item3, value));
            }

            totals[series] = total;
        }

        return totals.Where(total => total.Value.Count > 0)
                     .Select(total => new MatchMetricTotal(total.Key, total.Value.Sum, total.Value.Count, total.Value.Max))
                     .ToList();
    }
}
