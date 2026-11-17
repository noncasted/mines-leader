namespace Game.ClientTelemetry;

// The closed list of what a client tells about its device. Nothing else is kept: see the privacy rules in CONTRACT.md.
[GenerateSerializer]
public sealed record TelemetryDevice
{
    [Id(0)] public string? Platform { get; init; }
    [Id(1)] public string? BuildVersion { get; init; }
    [Id(2)] public string? Browser { get; init; }
    [Id(3)] public int? BrowserMajor { get; init; }
    [Id(4)] public string? Os { get; init; }
    [Id(5)] public int? OsMajor { get; init; }
    [Id(6)] public string? Gpu { get; init; }
    [Id(7)] public int? Cores { get; init; }
    [Id(8)] public double? MemoryClass { get; init; }
    [Id(9)] public int? ScreenWidth { get; init; }
    [Id(10)] public int? ScreenHeight { get; init; }
    [Id(11)] public double? Dpr { get; init; }
}
