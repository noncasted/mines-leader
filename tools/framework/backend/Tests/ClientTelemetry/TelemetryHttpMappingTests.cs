using FluentAssertions;
using Game.ClientTelemetry;
using Shared;
using Xunit;

namespace Framework.Tests.ClientTelemetry;

// What JsonUtility cannot write becomes what the socket would carry: NaN for an empty slot, null for an empty text, no duration, no
// previous session.
public class TelemetryHttpMappingTests
{
    [Fact]
    public void EmptySlots_AreNaN_AnIndexOutOfTheValuesIsIgnored()
    {
        var sample = TelemetryHttpMapping.ToSample(new HttpTelemetrySample { Time = 5, Values = [1, 0, 3], Empty = [1, 7, -1], Hidden = true });

        sample.Values.Should().HaveCount(3);
        sample.Values![0].Should().Be(1);
        double.IsNaN(sample.Values[1]).Should().BeTrue();
        sample.Values[2].Should().Be(3);
        sample.Hidden.Should().BeTrue();
    }

    [Fact]
    public void TheFlagsAndEmptyTexts_AreTheNullsOfTheSocket()
    {
        var batch = TelemetryHttpMapping.ToBatch(new HttpTelemetryBatch
        {
            Names = [new HttpTelemetryName { Name = "a", Unit = "" }],
            Marks = [new HttpTelemetryMark { Name = "m", Detail = "", HasDuration = false, DurationMs = 9 }, null!],
            Errors =
            [
                new HttpTelemetryError { Level = "Exception", Message = "x", Stack = "" },
                new HttpTelemetryError { Level = "Fatal", Message = "y" }
            ]
        });

        batch.Names![0].Unit.Should().BeNull();
        batch.Marks![0].Detail.Should().BeNull();
        batch.Marks[0].DurationMs.Should().BeNull();
        batch.Marks[1].Should().BeNull();
        batch.Errors![0].Level.Should().Be(SharedClientTelemetry.ErrorLevel.Exception);
        batch.Errors[0].Stack.Should().BeNull();
        ((byte)batch.Errors[1].Level).Should().Be(0);
        batch.Samples.Should().BeNull();
    }

    [Fact]
    public void NoPrevious_ByTheFlag()
    {
        var previous = new HttpTelemetryPreviousSession { SessionId = Guid.NewGuid() };

        TelemetryHttpMapping.ToSessionStart(new HttpTelemetrySessionStart { HasPrevious = false, Previous = previous }).Previous.Should().BeNull();
        TelemetryHttpMapping.ToSessionStart(new HttpTelemetrySessionStart { HasPrevious = true, Previous = previous }).Previous!.SessionId
            .Should().Be(previous.SessionId);
    }

    [Fact]
    public void IdleAfter_NotBelowLostAfter_Throws()
    {
        var act = () => new ClientTelemetryHttpLinks(
            null!,
            new ClientTelemetryHttpOptions { IdleAfter = TimeSpan.FromSeconds(120) },
            new ClientTelemetryStorageOptions(),
            TimeProvider.System,
            null!);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
