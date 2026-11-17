using Game.ClientTelemetry;

namespace Console.Infrastructure.ClientTelemetry;

// A run of samples taken while the page was in the background; of one sample From equals To.
public sealed record TelemetryHiddenSpan(long From, long To)
{
    public static IReadOnlyList<TelemetryHiddenSpan> Of(IReadOnlyList<TelemetryStoredSample> samples)
    {
        var spans = new List<TelemetryHiddenSpan>();
        TelemetryHiddenSpan? current = null;

        foreach (var sample in samples)
        {
            if (sample.Hidden)
            {
                current = new TelemetryHiddenSpan(current?.From ?? sample.Time, sample.Time);
                continue;
            }

            if (current != null)
                spans.Add(current);

            current = null;
        }

        if (current != null)
            spans.Add(current);

        return spans;
    }
}
