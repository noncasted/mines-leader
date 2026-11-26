namespace Game.ClientTelemetry;

// An empty text and 0 are unknown.
public sealed class HttpTelemetryDevice
{
    public string? Platform { get; init; }
    public string? BuildVersion { get; init; }
    public string? Browser { get; init; }
    public int BrowserMajor { get; init; }
    public string? Os { get; init; }
    public int OsMajor { get; init; }
    public string? Gpu { get; init; }
    public int Cores { get; init; }
    public double MemoryClass { get; init; }
    public int ScreenWidth { get; init; }
    public int ScreenHeight { get; init; }
    public double Dpr { get; init; }
}
