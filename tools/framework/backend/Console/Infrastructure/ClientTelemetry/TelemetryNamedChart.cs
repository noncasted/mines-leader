using Game.ClientTelemetry;

namespace Console.Infrastructure.ClientTelemetry;

public sealed record TelemetryNamedChart(TelemetryMetricName Name, TelemetryChart Chart);
