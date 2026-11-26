using FluentAssertions;
using Game.ClientTelemetry;
using Shared;
using Xunit;

namespace Framework.Tests.ClientTelemetry;

// The client is not trusted here either: what is out of form is dropped alone and counted, the start goes on.
public class TelemetryPreviousSessionParsingTests
{
    [Fact]
    public void None_IsNull()
    {
        TelemetryPreviousParser.Parse(null).Session.Should().BeNull();
    }

    [Fact]
    public void AnEmptyId_DropsTheWhole_AndCountsIt()
    {
        var parsed = TelemetryPreviousParser.Parse(new SharedClientTelemetry.PreviousSession { Tags = ["menu"] });

        parsed.Session.Should().BeNull();
        parsed.Rejected.Should().Be(1);
    }

    [Fact]
    public void BadParts_AreDroppedAlone()
    {
        var marks = Enumerable.Range(0, ClientTelemetryLimits.MaxPreviousMarks + 2)
                              .Select(index => new SharedClientTelemetry.Mark { Time = index, Name = $"mark.{index}" })
                              .Append(new SharedClientTelemetry.Mark { Time = 1, Name = "Player Ivan" })
                              .Append(new SharedClientTelemetry.Mark { Time = -1, Name = "page.freeze" })
                              .Append(null!)
                              .ToArray();

        var parsed = TelemetryPreviousParser.Parse(new SharedClientTelemetry.PreviousSession
        {
            SessionId = Guid.NewGuid(),
            Tags = ["run", "Bad Tag"],
            LastSampleTime = -5,
            SinceMs = -1,
            Marks = marks
        });

        var session = parsed.Session!;
        session.Tags.Should().Equal("run");
        session.LastSampleTime.Should().BeNull();
        session.SinceMs.Should().BeNull();
        session.Marks.Should().HaveCount(ClientTelemetryLimits.MaxPreviousMarks);
        session.Marks.Select(mark => mark.Name).Should().OnlyContain(name => name.StartsWith("mark."));
        session.Marks[^1].Name.Should().Be($"mark.{ClientTelemetryLimits.MaxPreviousMarks + 1}");
        parsed.Rejected.Should().BeGreaterThan(0);
    }

    // The marks of a load carry its key and time: kept the way a batch keeps them, a detail scrubbed and cut, a negative time dropped.
    [Fact]
    public void TheDetailAndDuration_AreKeptLikeTheOnesOfABatch()
    {
        var parsed = TelemetryPreviousParser.Parse(new SharedClientTelemetry.PreviousSession
        {
            SessionId = Guid.NewGuid(),
            LastSampleTime = -1,
            SinceMs = -1,
            Marks =
            [
                new SharedClientTelemetry.Mark { Time = 1000, Name = "load.start", Detail = "Group: MusicRunAudio" },
                new SharedClientTelemetry.Mark { Time = 6000, Name = "load.slow", Detail = "Group: MusicRunAudio", DurationMs = 5000 },
                new SharedClientTelemetry.Mark { Time = 7000, Name = "load.failed", Detail = "user@mail.com " + new string('x', 200), DurationMs = 0 },
                new SharedClientTelemetry.Mark { Time = 8000, Name = "load.end", Detail = "Group: MenuAudio", DurationMs = -5 },
                new SharedClientTelemetry.Mark { Time = 9000, Name = "page.pagehide", Detail = "a\nb" }
            ]
        });

        var marks = parsed.Session!.Marks;
        marks.Select(mark => mark.Name).Should().Equal("load.start", "load.slow", "load.failed", "page.pagehide");
        marks[0].Should().Be(new TelemetryMark(1000, "load.start", "Group: MusicRunAudio"));
        marks[1].Should().Be(new TelemetryMark(6000, "load.slow", "Group: MusicRunAudio", 5000));
        marks[2].Detail!.Length.Should().BeLessThanOrEqualTo(ClientTelemetryLimits.MaxMarkDetailLength);
        marks[2].Detail.Should().NotContain("user@mail.com");
        marks[2].DurationMs.Should().Be(0);
        marks[3].Detail.Should().BeNull();
        parsed.Rejected.Should().Be(1);
    }

    // The last samples of a frame: values by the names, a missing value absent, the time of that session.
    [Fact]
    public void TheSamples_AreKeptByTheirNames()
    {
        var parsed = TelemetryPreviousParser.Parse(Previous(["frame.dt.max", "render.draw_calls"],
            new SharedClientTelemetry.Sample { Time = 30_000, Values = [41.5, 120] },
            new SharedClientTelemetry.Sample { Time = 31_000, Values = [double.NaN, 95], Hidden = true },
            new SharedClientTelemetry.Sample { Time = 32_000, Values = [55] },
            new SharedClientTelemetry.Sample { Time = 33_000 }));

        var samples = parsed.Session!.Samples;
        samples.Select(sample => (sample.Time, sample.Hidden)).Should().Equal((30_000L, false), (31_000L, true), (32_000L, false), (33_000L, false));
        samples[0].Values.Should().BeEquivalentTo(new Dictionary<string, double> { ["frame.dt.max"] = 41.5, ["render.draw_calls"] = 120 });
        samples[1].Values.Should().BeEquivalentTo(new Dictionary<string, double> { ["render.draw_calls"] = 95 });
        samples[2].Values.Should().BeEquivalentTo(new Dictionary<string, double> { ["frame.dt.max"] = 55 });
        samples[3].Values.Should().BeEmpty();
        parsed.Rejected.Should().Be(0, "NaN is the way to say no value");
    }

    [Fact]
    public void NoLists_AreNoSamples()
    {
        var parsed = TelemetryPreviousParser.Parse(Previous(null, null));

        parsed.Session!.Samples.Should().BeEmpty();
        parsed.Rejected.Should().Be(0);
    }

    // Each bad part is counted once: a name with its values, a sample, an infinite value, the values past the names.
    [Fact]
    public void ABadName_DropsItsValues_AndIsCounted()
    {
        var parsed = TelemetryPreviousParser.Parse(Previous(["frame.dt.max", "Player Ivan", null!, "frame.dt.max", "frame.cpu.main"],
            new SharedClientTelemetry.Sample { Time = 1000, Values = [1, 2, 3, 4, 5, 6, 7] }));

        parsed.Session!.Samples.Single().Values.Should().BeEquivalentTo(new Dictionary<string, double> { ["frame.dt.max"] = 1, ["frame.cpu.main"] = 5 });
        parsed.Rejected.Should().Be(4, "two names out of form, a repeated name and the values past the names");
    }

    [Fact]
    public void AnInfiniteValue_IsNoValue_AndIsCounted()
    {
        var parsed = TelemetryPreviousParser.Parse(Previous(["frame.dt.max", "frame.cpu.main"],
            new SharedClientTelemetry.Sample { Time = 1000, Values = [double.PositiveInfinity, 3] }));

        parsed.Session!.Samples.Single().Values.Should().BeEquivalentTo(new Dictionary<string, double> { ["frame.cpu.main"] = 3 });
        parsed.Rejected.Should().Be(1);
    }

    [Fact]
    public void ASampleOutOfTime_IsDropped_AndCounted()
    {
        var parsed = TelemetryPreviousParser.Parse(Previous(["frame.dt.max"],
            new SharedClientTelemetry.Sample { Time = -1, Values = [1] },
            null!,
            new SharedClientTelemetry.Sample { Time = ClientTelemetryLimits.MaxPreviousMs + 1, Values = [2] },
            new SharedClientTelemetry.Sample { Time = ClientTelemetryLimits.MaxPreviousMs, Values = [3] }));

        parsed.Session!.Samples.Select(sample => sample.Time).Should().Equal(ClientTelemetryLimits.MaxPreviousMs);
        parsed.Rejected.Should().Be(3);
    }

    // The last ones are kept: the end of the session is what it is reported for.
    [Fact]
    public void TheListsOverTheLimits_AreCut_AndCounted()
    {
        var names = Enumerable.Range(0, ClientTelemetryLimits.MaxPreviousSampleNames + 3).Select(index => $"metric.{index}").ToArray();
        var samples = Enumerable.Range(0, ClientTelemetryLimits.MaxPreviousSamples + 2)
                                .Select(index => new SharedClientTelemetry.Sample { Time = index, Values = names.Select(_ => (double)index).ToArray() })
                                .ToArray();

        var parsed = TelemetryPreviousParser.Parse(Previous(names, samples));

        var kept = parsed.Session!.Samples;
        kept.Should().HaveCount(ClientTelemetryLimits.MaxPreviousSamples);
        kept[0].Time.Should().Be(2);
        kept[^1].Time.Should().Be(ClientTelemetryLimits.MaxPreviousSamples + 1);
        kept.Should().OnlyContain(sample => sample.Values.Count == ClientTelemetryLimits.MaxPreviousSampleNames);
        kept[0].Values.Keys.Should().NotContain($"metric.{ClientTelemetryLimits.MaxPreviousSampleNames}");
        parsed.Rejected.Should().Be(3 + 2, "the names over the limit and the oldest samples, the values of a cut name with it");
    }

    private static SharedClientTelemetry.PreviousSession Previous(string[]? names, params SharedClientTelemetry.Sample[]? samples)
    {
        return new SharedClientTelemetry.PreviousSession
        {
            SessionId = Guid.NewGuid(),
            LastSampleTime = -1,
            SinceMs = -1,
            SampleNames = names,
            Samples = samples
        };
    }
}
