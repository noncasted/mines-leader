namespace Console.Infrastructure.ClientTelemetry;

// A run of points of one kind without a gap. Hidden: taken while the page was in the background.
public sealed record TelemetryChartSegment(bool Hidden, IReadOnlyList<TelemetryChartPoint> Points);
