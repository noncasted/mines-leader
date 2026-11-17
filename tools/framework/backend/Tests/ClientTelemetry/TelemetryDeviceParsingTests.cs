using FluentAssertions;
using Game.ClientTelemetry;
using Shared;
using Xunit;

namespace Framework.Tests.ClientTelemetry;

public class TelemetryDeviceParsingTests
{
    [Theory]
    [InlineData("Chrome 120.0.6099.144")] // finer than the major version
    [InlineData("Mozilla/5.0 (Linux; Android 14; Pixel 8)")]
    [InlineData("ivan@mail.ru")]
    public void BrowserAndOsWithAVersionOrAFreeText_AreDropped(string value)
    {
        Parse(new SharedClientTelemetry.Device { Browser = value, Os = value }).Should().Be(new TelemetryDevice());
    }

    [Theory]
    [InlineData("ivan@mail.ru")]
    [InlineData("Ivan Petrov")]
    public void StringWithForbiddenCharacters_IsDropped(string value)
    {
        Parse(new SharedClientTelemetry.Device { Platform = value, BuildVersion = value }).Should().Be(new TelemetryDevice());
    }

    [Fact]
    public void GpuWithPunctuationOfRealModels_IsKept()
    {
        const string gpu = "ANGLE (Qualcomm, Adreno (TM) 640, OpenGL ES 3.2)";

        Parse(new SharedClientTelemetry.Device { Gpu = gpu }).Should().Be(new TelemetryDevice { Gpu = gpu });
    }

    [Fact]
    public void TooLongString_IsDropped()
    {
        Parse(new SharedClientTelemetry.Device { Gpu = new string('a', 129), BuildVersion = new string('1', 33) }).Should().Be(new TelemetryDevice());
    }

    [Fact]
    public void NumbersOutOfRange_AreDropped()
    {
        Parse(new SharedClientTelemetry.Device
        {
            BrowserMajor = -1, OsMajor = 100000, Cores = 0, MemoryClass = 3.7, ScreenWidth = 0, ScreenHeight = 100000, Dpr = 100
        }).Should().Be(new TelemetryDevice());
    }

    [Theory]
    [InlineData(0.25)]
    [InlineData(1)]
    [InlineData(64)]
    public void MemoryClass_IsAPowerOfTwo(double memoryClass)
    {
        Parse(new SharedClientTelemetry.Device { MemoryClass = memoryClass }).Should().Be(new TelemetryDevice { MemoryClass = memoryClass });
    }

    [Fact]
    public void NoDevice_IsAnEmptyOne()
    {
        Parse(null).Should().Be(new TelemetryDevice());
    }

    private static TelemetryDevice Parse(SharedClientTelemetry.Device? device)
    {
        return TelemetryDeviceParser.Parse(device);
    }
}
