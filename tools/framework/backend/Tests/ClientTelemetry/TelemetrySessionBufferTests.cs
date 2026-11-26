using FluentAssertions;
using Game.ClientTelemetry;
using Infrastructure.State;
using Newtonsoft.Json;
using Xunit;

namespace Framework.Tests.ClientTelemetry;

public class TelemetrySessionBufferTests
{
    private static readonly DateTime _now = new(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);

    private static readonly ClientTelemetryStorageOptions _small = new() { MaxSamples = 3, MaxErrors = 1, MaxMarks = 2 };

    [Fact]
    public void OverTheCapacity_TheOldestGoAndAreCounted()
    {
        var buffer = TelemetrySessionBuffer.Empty(_small);

        for (var seq = 1; seq <= 5; seq++)
        {
            buffer.Apply(Request(seq), _now);
        }

        buffer.Samples.Items.Select(sample => sample.Seq).Should().Equal(3, 4, 5);
        buffer.Samples.Dropped.Should().Be(2);
        buffer.Errors.Items.Select(error => error.Seq).Should().Equal(5);
        buffer.Errors.Dropped.Should().Be(4);
        buffer.Marks.Items.Select(mark => mark.Seq).Should().Equal(4, 5);
        buffer.Marks.Dropped.Should().Be(3);

        var digest = TelemetrySummaryCalculator.Digest(buffer);
        digest.DroppedSamples.Should().Be(2);
        digest.DurationMs.Should().Be(5);
    }

    [Fact]
    public void Digest_LeavesTheHiddenOutOfTheSummary_AndCountsTheRepeats()
    {
        var buffer = TelemetrySessionBuffer.Empty(new ClientTelemetryStorageOptions());

        buffer.Apply(new TelemetryBatch
        {
            Names = [new TelemetryMetricName(0, "memory.heap", "bytes")],
            NameSlots = 1,
            Samples = [new TelemetrySample(10, [100], false), new TelemetrySample(20, [900], true), new TelemetrySample(30, [150], false)],
            Errors = [new TelemetryError(40, TelemetryErrorLevel.Error, "a", "", 5, null)],
            Marks = []
        }, _now);

        var digest = TelemetrySummaryCalculator.Digest(buffer);

        digest.DurationMs.Should().Be(40);
        digest.ErrorRows.Should().Be(1);
        digest.ErrorCount.Should().Be(5);
        digest.Summary.Samples.Should().Be(2);
        digest.Summary.HiddenSamples.Should().Be(1);

        var metric = digest.Summary.Metrics.Single();
        metric.Max.Should().Be(150);
        metric.Growth.Should().Be(50);
    }

    [Fact]
    public void TheLog_GoesThroughTheJsonOfTheStates()
    {
        var buffer = TelemetrySessionBuffer.Empty(_small);
        buffer.Apply(Request(1), _now);
        buffer.Apply(Request(2), _now);

        var settings = JsonStateSettings.CreateBase();
        var json = JsonConvert.SerializeObject(TelemetrySessionLogState.Of(buffer), settings);
        var log = JsonConvert.DeserializeObject<TelemetrySessionLogState>(json, settings)!;

        json.Should().Contain("[1,1,false,", "a sample is an array");
        log.Samples.Should().HaveCount(2);
        log.Samples[1].Seq.Should().Be(2);
        log.Samples[1].ReceivedAt.Should().Be(_now);
        log.Samples[1].Values.Should().Equal(2, null);
        log.Errors[0].Level.Should().Be(TelemetryErrorLevel.Error);
        log.Marks[0].Name.Should().Be("m");
        log.DroppedErrors.Should().Be(1);
    }

    [Fact]
    public void APage_NeverCutsOneSeq()
    {
        var rows = new[] { 1L, 2, 2, 2, 3 };

        TelemetryRows.Page(rows, 0, 2, row => row).Should().Equal(1, 2, 2, 2);
        TelemetryRows.Page(rows, 2, 10, row => row).Should().Equal(3);
        TelemetryRows.Page(rows, 3, 10, row => row).Should().BeEmpty();
        TelemetryRows.Page(rows, 0, int.MaxValue, row => row).Should().HaveCount(5);
    }

    private static TelemetryBatch Request(long seq)
    {
        return new TelemetryBatch
        {
            Names = seq == 1 ? [new TelemetryMetricName(0, "a", null)] : [],
            NameSlots = seq == 1 ? 2 : 0,
            Samples = [new TelemetrySample(seq, [seq, null], false)],
            Errors = [new TelemetryError(seq, TelemetryErrorLevel.Error, "e", "", 1, null)],
            Marks = [new TelemetryMark(seq, "m")]
        };
    }
}
