using System.Text.RegularExpressions;
using Shared;

namespace Game.ClientTelemetry;

// A field of a wrong form or range is dropped alone: it says nothing about the metrics of the session.
internal static class TelemetryDeviceParser
{
    private const RegexOptions _options = RegexOptions.NonBacktracking | RegexOptions.CultureInvariant;
    private const int _maxScreenSide = 16384;
    private const int _maxCores = 1024;
    private const int _maxMajorVersion = 9999;
    private const double _maxDpr = 16;

    private static readonly Regex _platform = new(@"\A[A-Za-z0-9._\-]{1,32}\z", _options);
    private static readonly Regex _buildVersion = new(@"\A[A-Za-z0-9._+\-]{1,32}\z", _options);

    // No digits: a browser or an OS with a version finer than the major one has no way through.
    private static readonly Regex _productName = new(@"\A[A-Za-z][A-Za-z .\-]{0,31}\z", _options);
    private static readonly Regex _gpu = new(@"\A[A-Za-z0-9 ()_.,+/\-]{1,128}\z", _options);

    private static readonly HashSet<double> _memoryClasses = [0.25, 0.5, 1, 2, 4, 8, 16, 32, 64, 128, 256, 512, 1024];

    // Null: a client that told nothing, the session still starts.
    public static TelemetryDevice Parse(SharedClientTelemetry.Device? device)
    {
        if (device == null)
            return new TelemetryDevice();

        return new TelemetryDevice
        {
            Platform = Text(device.Platform, _platform),
            BuildVersion = Text(device.BuildVersion, _buildVersion),
            Browser = Text(device.Browser, _productName),
            BrowserMajor = Integer(device.BrowserMajor, _maxMajorVersion),
            Os = Text(device.Os, _productName),
            OsMajor = Integer(device.OsMajor, _maxMajorVersion),
            Gpu = Text(device.Gpu, _gpu),
            Cores = Integer(device.Cores, _maxCores),
            MemoryClass = _memoryClasses.Contains(device.MemoryClass) ? device.MemoryClass : null,
            ScreenWidth = Integer(device.ScreenWidth, _maxScreenSide),
            ScreenHeight = Integer(device.ScreenHeight, _maxScreenSide),
            Dpr = device.Dpr > 0 && device.Dpr <= _maxDpr ? device.Dpr : null
        };
    }

    private static string? Text(string? text, Regex form)
    {
        return text != null && form.IsMatch(text) ? text : null;
    }

    private static int? Integer(int number, int max)
    {
        return number >= 1 && number <= max ? number : null;
    }
}
