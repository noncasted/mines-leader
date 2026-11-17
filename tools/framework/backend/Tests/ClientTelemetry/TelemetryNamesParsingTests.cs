using FluentAssertions;
using Game.ClientTelemetry;
using Shared;
using Xunit;

namespace Framework.Tests.ClientTelemetry;

public class TelemetryNamesParsingTests
{
    [Theory]
    [InlineData("a")]
    [InlineData("frame.dt_avg")]
    [InlineData("gpu0.time_95")]
    public void AllowedName_IsValid(string name)
    {
        TelemetryNames.IsValidName(name).Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("Frame")]
    [InlineData("frame dt")]
    [InlineData("frame-dt")]
    [InlineData("ivan@mail.ru")]
    [InlineData("игрок")]
    [InlineData("frame\n")]
    public void ForbiddenName_IsNotValid(string name)
    {
        TelemetryNames.IsValidName(name).Should().BeFalse();
    }

    [Fact]
    public void NameLength_IsLimited()
    {
        TelemetryNames.IsValidName(new string('a', ClientTelemetryLimits.MaxNameLength)).Should().BeTrue();
        TelemetryNames.IsValidName(new string('a', ClientTelemetryLimits.MaxNameLength + 1)).Should().BeFalse();
    }

    [Fact]
    public void BadName_IsDropped_AndKeepsItsSlot()
    {
        var batch = TelemetryBatchParser.Parse(TelemetryWire.Batch(Guid.Empty, ["first", "Ivan Petrov", "third"]));

        batch.NameSlots.Should().Be(3);
        batch.Names.Select(name => (name.Slot, name.Name)).Should().Equal((0, "first"), (2, "third"));
    }

    [Fact]
    public void BadUnit_IsDropped_TheNameStays()
    {
        var batch = TelemetryBatchParser.Parse(new SharedClientTelemetry.Batch
        {
            Names =
            [
                new SharedClientTelemetry.MetricName { Name = "a", Unit = "Ivan Petrov" },
                new SharedClientTelemetry.MetricName { Name = "b", Unit = new string('a', ClientTelemetryLimits.MaxUnitLength + 1) },
                new SharedClientTelemetry.MetricName { Name = "c", Unit = "ms" }
            ]
        });

        batch.Names.Should().Equal(new TelemetryMetricName(0, "a", null), new TelemetryMetricName(1, "b", null), new TelemetryMetricName(2, "c", "ms"));
    }

    [Fact]
    public void BadMark_IsDropped()
    {
        var batch = TelemetryBatchParser.Parse(TelemetryWire.Batch(Guid.Empty, marks: [(1, "stage.start"), (2, "Ivan joined")]));

        batch.Marks.Should().Equal(new TelemetryMark(1, "stage.start"));
    }

    [Fact]
    public void TheDetailAndTheDurationOfAMark_AreKept_AndTheDetailIsScrubbed()
    {
        var batch = TelemetryBatchParser.Parse(Marks(
            new SharedClientTelemetry.Mark { Time = 5, Name = "load.end", Detail = "Audio: MusicRunAudio", DurationMs = 1200 },
            new SharedClientTelemetry.Mark { Time = 6, Name = "load.failed", Detail = "Scene: https://cdn.example.com/run.bundle?k=1" },
            new SharedClientTelemetry.Mark { Time = 7, Name = "stage.start" }));

        batch.Marks.Should().Equal(
            new TelemetryMark(5, "load.end", "Audio: MusicRunAudio", 1200),
            new TelemetryMark(6, "load.failed", "Scene: <url>", null),
            new TelemetryMark(7, "stage.start", null, null));
    }

    [Fact]
    public void ABadDetail_IsDropped_TheMarkStays_ALongOneIsCut()
    {
        var batch = TelemetryBatchParser.Parse(Marks(
            new SharedClientTelemetry.Mark { Time = 1, Name = "load.end", Detail = "a\nb" },
            new SharedClientTelemetry.Mark { Time = 2, Name = "load.end", Detail = " " },
            new SharedClientTelemetry.Mark { Time = 3, Name = "load.end", Detail = string.Concat(Enumerable.Repeat("word ", 60)) }));

        batch.Marks[0].Detail.Should().BeNull();
        batch.Marks[1].Detail.Should().BeNull();
        batch.Marks[2].Detail.Should().HaveLength(ClientTelemetryLimits.MaxMarkDetailLength);
    }

    [Fact]
    public void ANegativeDurationOfAMark_RejectsTheBatch()
    {
        TelemetryBatchParser.Parse(Marks(new SharedClientTelemetry.Mark { Time = 1, Name = "load.end", DurationMs = -1 }))
            .Rejection.Should().Be(ClientTelemetryRejection.Malformed);
    }

    private static SharedClientTelemetry.Batch Marks(params SharedClientTelemetry.Mark[] marks)
    {
        return new SharedClientTelemetry.Batch { SessionId = Guid.Empty, Marks = marks };
    }

    [Fact]
    public void Tags_GoByTheRulesOfTheNames_AndTheRestIsCounted()
    {
        var over = Enumerable.Range(0, ClientTelemetryLimits.MaxTagsPerSession + 2).Select(index => $"t{index}");
        var tags = TelemetryTagsParser.Parse(["menu", "Ivan Petrov", null!, .. over]);

        tags.Accepted.Should().HaveCount(ClientTelemetryLimits.MaxTagsPerSession).And.StartWith("menu");
        tags.Rejected.Should().Be(5);
        TelemetryTagsParser.Parse(null).Should().BeEquivalentTo(new TelemetrySessionTags([], 0));
    }
}
