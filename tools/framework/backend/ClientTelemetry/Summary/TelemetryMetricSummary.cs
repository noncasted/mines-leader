namespace Game.ClientTelemetry;

// Growth: the last value minus the first one in the window, only for a metric in bytes (a leak shows there).
[GenerateSerializer]
public sealed record TelemetryMetricSummary(
    string Name,
    string? Unit,
    int Count,
    double Average,
    double P50,
    double P95,
    double Min,
    double Max,
    double? Growth);
