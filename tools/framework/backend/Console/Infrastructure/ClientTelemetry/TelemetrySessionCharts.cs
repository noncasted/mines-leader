using Game.ClientTelemetry;

namespace Console.Infrastructure.ClientTelemetry;

// Everything the card draws over time. A metric without a value in any sample has no chart.
public sealed record TelemetrySessionCharts(TelemetryTimeline Timeline, IReadOnlyList<TelemetryHiddenSpan> HiddenSpans, IReadOnlyList<TelemetryNamedChart> Charts)
{
    private const int _chartBuckets = 300;
    private const int _timelineBuckets = 200;

    public static TelemetrySessionCharts Of(TelemetrySessionLog log, IEnumerable<TelemetryMetricName> names)
    {
        return new TelemetrySessionCharts(
            TelemetryTimeline.Of(log.Samples, log.Errors, log.Marks, _timelineBuckets),
            TelemetryHiddenSpan.Of(log.Samples),
            names.Select(name => new TelemetryNamedChart(name, TelemetryChart.Of(log.Samples, name.Slot, _chartBuckets)))
                 .Where(item => item.Chart.Segments.Count > 0)
                 .ToArray());
    }
}
