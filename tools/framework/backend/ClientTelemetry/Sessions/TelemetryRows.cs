namespace Game.ClientTelemetry;

internal static class TelemetryRows
{
    // The rows go in the order of seq. The page ends with the seq of the row at the limit, whole.
    public static IReadOnlyList<T> Page<T>(IReadOnlyList<T> rows, long afterSeq, int limit, Func<T, long> seq)
    {
        var start = FirstAfter(rows, afterSeq, seq);

        if (start == rows.Count)
            return [];

        var end = Math.Min((long)start + limit, rows.Count);

        while (end < rows.Count && seq(rows[(int)end]) == seq(rows[(int)end - 1]))
            end++;

        return rows.Skip(start).Take((int)end - start).ToArray();
    }

    public static IReadOnlyList<TelemetryStoredSample> Window(IReadOnlyList<TelemetryStoredSample> samples, long from, long to)
    {
        return samples.Where(sample => sample.Time >= from && sample.Time <= to).ToArray();
    }

    private static int FirstAfter<T>(IReadOnlyList<T> rows, long afterSeq, Func<T, long> seq)
    {
        var low = 0;
        var high = rows.Count;

        while (low < high)
        {
            var middle = low + (high - low) / 2;

            if (seq(rows[middle]) > afterSeq)
                high = middle;
            else
                low = middle + 1;
        }

        return low;
    }
}
