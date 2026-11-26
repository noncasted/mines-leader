using Console.Infrastructure.ClientTelemetry;
using Game.ClientTelemetry;
using Xunit;
using static Framework.Tests.ClientTelemetry.TelemetryConsoleSamples;

namespace Framework.Tests.ClientTelemetry;

public class TelemetryConsoleChartTests
{
    [Fact]
    public void Chart_SeparatesHiddenSamples_AndBreaksOnAMissingValue()
    {
        TelemetryStoredSample[] samples =
        [
            Sample(1, 0, 60), Sample(1, 1000, 50), Sample(1, 2000, null), Sample(1, 3000, 40), Sample(1, 4000, 5, hidden: true), Sample(1, 5000, 6, hidden: true), Sample(1, 6000, 70)
        ];

        var chart = TelemetryChart.Of(samples, 0, 100);

        Assert.Equal(
            [(false, 2), (false, 1), (true, 2), (false, 1)],
            chart.Segments.Select(segment => (segment.Hidden, segment.Points.Count)));
        Assert.Equal(5, chart.Min);
        Assert.Equal(70, chart.Max);
    }

    [Fact]
    public void Chart_OfASlotOutOfASample_HasNoPoint()
    {
        Assert.Empty(TelemetryChart.Of([Sample(1, 0, 1)], 3, 100).Segments);
    }

    [Fact]
    public void Chart_OverTheBuckets_KeepsTheSpikeAndTheOrderOfTime()
    {
        var samples = Enumerable.Range(0, 20_000).Select(i => Sample(1, i, i == 12_345 ? 1000 : 60)).ToArray();

        var points = Assert.Single(TelemetryChart.Of(samples, 0, 100).Segments).Points;

        Assert.True(points.Count <= 202, $"{points.Count} points");
        Assert.Contains(points, point => point is { Time: 12_345, Value: 1000 });
        Assert.Equal(points.Select(point => point.Time).Order(), points.Select(point => point.Time));
        Assert.Equal(0, points[0].Time);
        Assert.Equal(19_999, points[^1].Time);
    }

    [Fact]
    public void HiddenSpans_AreTheRunsOfHiddenSamples()
    {
        TelemetryStoredSample[] samples =
        [
            Sample(1, 0, 1), Sample(1, 1000, 1, hidden: true), Sample(1, 2000, 1, hidden: true), Sample(1, 3000, 1), Sample(1, 4000, 1, hidden: true)
        ];

        Assert.Equal([new TelemetryHiddenSpan(1000, 2000), new TelemetryHiddenSpan(4000, 4000)], TelemetryHiddenSpan.Of(samples));
    }

    [Fact]
    public void Geometry_MapsTheTimeAndAFlatLineToTheMiddle()
    {
        var geometry = new TelemetryChartGeometry(2000, 0, 100);
        var flat = new TelemetryChartGeometry(2000, 5, 5);

        Assert.Equal(0, geometry.X(0));
        Assert.Equal(TelemetryChartGeometry.Width, geometry.X(2000));
        Assert.Equal(TelemetryChartGeometry.Height, geometry.Y(0));
        Assert.Equal(0, geometry.Y(100));
        Assert.Equal(TelemetryChartGeometry.Height / 2.0, flat.Y(5));
    }

    [Fact]
    public void Geometry_Path_UsesAPointForTheDecimal_ADotForOneValue()
    {
        var geometry = new TelemetryChartGeometry(2000, 0, 100);

        Assert.Equal("M250 50 L500 50", geometry.Path([new TelemetryChartPoint(500, 50), new TelemetryChartPoint(1000, 50)]));
        Assert.Equal("M250.5 50 h0.1", new TelemetryChartGeometry(2000, 0, 100).Path([new TelemetryChartPoint(501, 50)]));
    }

    [Fact]
    public void Timeline_EndsWithTheLatestRowOfAnyKind()
    {
        Assert.Equal(9000, TelemetryTimeline.Of([Sample(1, 1000, 1)], [Error(1, 2000, "a")], [Mark(1, 9000, "m")], 10).End);
        Assert.Equal(1, TelemetryTimeline.Of([], [], [], 10).End);
    }

    [Fact]
    public void Timeline_JoinsTheEventsOfOneBucket_WithRepeatsAndLabels()
    {
        var timeline = TelemetryTimeline.Of(
            [Sample(1, 10_000, 1)],
            [Error(1, 100, "a", "first", repeat: 3), Error(1, 200, "b", "second"), Error(1, 5000, "a", "third")],
            [Mark(1, 100, "level.start"), Mark(1, 200, "level.end")],
            10);

        Assert.Equal(2, timeline.Ticks.Count(tick => tick.Kind == TelemetryTimelineKind.Error));

        var errors = timeline.Ticks.First(tick => tick.Kind == TelemetryTimelineKind.Error);
        Assert.Equal(4, errors.Count);
        Assert.Contains("first", errors.Label);
        Assert.Contains("second", errors.Label);

        var marks = Assert.Single(timeline.Ticks, tick => tick.Kind == TelemetryTimelineKind.Mark);
        Assert.Equal(2, marks.Count);
        Assert.Equal(100, marks.Time);
        Assert.Contains("level.end", marks.Label);
    }

    [Fact]
    public void Timeline_CutsALongLabel_AndTellsTheRest()
    {
        var marks = Enumerable.Range(0, 9).Select(i => Mark(1, i, $"mark{i}")).ToArray();

        var tick = Assert.Single(TelemetryTimeline.Of([Sample(1, 10_000, 1)], [], marks, 2).Ticks);

        Assert.Contains("mark0", tick.Label);
        Assert.DoesNotContain("mark8", tick.Label);
        Assert.Contains("+6", tick.Label);
    }

    [Fact]
    public void Axis_DividesTheScale()
    {
        Assert.Equal([0, 250, 500, 750, 1000], TelemetryTimeline.Of([Sample(1, 1000, 1)], [], [], 10).Axis(4));
    }
}
