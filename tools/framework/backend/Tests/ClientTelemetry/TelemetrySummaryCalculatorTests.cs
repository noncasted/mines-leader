using FluentAssertions;
using Game.ClientTelemetry;
using Xunit;

namespace Framework.Tests.ClientTelemetry;

public class TelemetrySummaryCalculatorTests
{
    private static readonly TelemetryMetricName _frame = new(0, "frame.dt", "ms");
    private static readonly TelemetryMetricName _memory = new(1, "memory.heap", "bytes");

    [Fact]
    public void Statistics_OfAMetric()
    {
        var samples = Enumerable.Range(1, 100).Select(value => Sample(value, value)).ToArray();

        var metric = Calculate([_frame], samples).Metrics.Single();

        metric.Name.Should().Be("frame.dt");
        metric.Unit.Should().Be("ms");
        metric.Count.Should().Be(100);
        metric.Average.Should().Be(50.5);
        metric.P50.Should().Be(50);
        metric.P95.Should().Be(95);
        metric.Min.Should().Be(1);
        metric.Max.Should().Be(100);
    }

    // A client is not trusted: an average or a growth past the limit of a double is a number no JSON holds, and one such row broke
    // the list of the sessions for everybody (a 500 on every reading).
    [Fact]
    public void ValuesNearTheLimit_EveryStatisticStaysFinite()
    {
        var metric = Calculate([new TelemetryMetricName(0, "memory.huge", "bytes")], [Sample(double.MaxValue), Sample(double.MaxValue), Sample(-double.MaxValue)]).Metrics.Single();

        Assert.True(double.IsFinite(metric.Average));
        Assert.Equal(double.MaxValue / 3, metric.Average, 1e300);
        Assert.Null(metric.Growth);
    }

    [Fact]
    public void Percentile_IsAValueThatWasSeen_OnASmallSet()
    {
        var metric = Calculate([_frame], [Sample(30), Sample(10), Sample(20)]).Metrics.Single();

        metric.P50.Should().Be(20);
        metric.P95.Should().Be(30);
    }

    [Fact]
    public void OnePoint_IsEveryStatistic()
    {
        var metric = Calculate([_frame], [Sample(7)]).Metrics.Single();

        metric.Count.Should().Be(1);
        new[] { metric.Average, metric.P50, metric.P95, metric.Min, metric.Max }.Should().AllBeEquivalentTo(7d);
    }

    [Fact]
    public void HiddenSamples_AreOutOfEveryStatistic_AndCounted()
    {
        var samples = new[] { Sample(10), Sample(10_000, hidden: true), Sample(20) };

        var summary = Calculate([_frame], samples);

        summary.Samples.Should().Be(2);
        summary.HiddenSamples.Should().Be(1);
        summary.Metrics.Single().Max.Should().Be(20);
        summary.Metrics.Single().Average.Should().Be(15);
    }

    [Fact]
    public void Growth_OnlyForBytes_FromTheFirstToTheLastValue()
    {
        var samples = new[] { Sample(10, 100), Sample(5, 900), Sample(1, 400) };

        var summary = Calculate([_frame, _memory], samples);

        summary.Metrics.Single(metric => metric.Name == "memory.heap").Growth.Should().Be(300);
        summary.Metrics.Single(metric => metric.Name == "frame.dt").Growth.Should().BeNull();
    }

    [Fact]
    public void Growth_LeavesTheHiddenAndTheEmptyValuesOut()
    {
        var samples = new[] { Sample(0, null), Sample(0, 100), Sample(0, 999, hidden: true), Sample(0, 150), Sample(0, null) };

        Calculate([_frame, _memory], samples).Metrics.Single(metric => metric.Name == "memory.heap").Growth.Should().Be(50);
    }

    [Fact]
    public void EmptyValues_AreNotPoints()
    {
        var samples = new[] { Sample(1, null), Sample(2, 50) };

        var summary = Calculate([_frame, _memory], samples);

        summary.Metrics.Single(metric => metric.Name == "memory.heap").Count.Should().Be(1);
        summary.Metrics.Single(metric => metric.Name == "frame.dt").Count.Should().Be(2);
    }

    [Fact]
    public void ValuesOfASlotWithoutAName_BelongToNothing_AndAShortSampleLeavesTheRestEmpty()
    {
        var named = new TelemetryMetricName(2, "named", null);
        var samples = new[] { Sample(1, 2, 3), Sample(1) };

        var metric = Calculate([named], samples).Metrics.Single();

        metric.Count.Should().Be(1);
        metric.Max.Should().Be(3);
    }

    [Fact]
    public void AMetricWithoutPoints_IsNotListed()
    {
        var samples = new[] { Sample(1, 50, hidden: true) };

        var summary = Calculate([_frame, _memory], samples);

        summary.Samples.Should().Be(0);
        summary.Metrics.Should().BeEmpty();
    }

    [Fact]
    public void NoSamples_IsAnEmptySummary()
    {
        var summary = Calculate([_frame], []);

        summary.Samples.Should().Be(0);
        summary.HiddenSamples.Should().Be(0);
        summary.Metrics.Should().BeEmpty();
    }

    private static TelemetrySessionSummary Calculate(IReadOnlyList<TelemetryMetricName> names, IReadOnlyList<TelemetryStoredSample> samples)
    {
        return TelemetrySummaryCalculator.Calculate(names, samples);
    }

    private static TelemetryStoredSample Sample(params double?[] values)
    {
        return new TelemetryStoredSample(1, 0, values, false, DateTime.UnixEpoch);
    }

    private static TelemetryStoredSample Sample(double? first, double? second, bool hidden)
    {
        return new TelemetryStoredSample(1, 0, [first, second], hidden, DateTime.UnixEpoch);
    }

    private static TelemetryStoredSample Sample(double? first, bool hidden)
    {
        return new TelemetryStoredSample(1, 0, [first], hidden, DateTime.UnixEpoch);
    }
}
