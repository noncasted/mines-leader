using FluentAssertions;
using Game.ClientTelemetry;
using Xunit;

namespace Framework.Tests.ClientTelemetry;

public class TelemetryStartupAnalysisTests
{
    private static TelemetryStartupSpan Span(int parent, double start, double duration)
    {
        return new TelemetryStartupSpan(parent, "a", start, duration, 0, false, 0);
    }

    [Fact]
    public void OwnTime_IsTheDurationWithoutTheChildren_AndDepthComesFromTheParents()
    {
        var spans = TelemetryStartupAnalysis.Of([Span(-1, 0, 100), Span(0, 10, 20), Span(1, 12, 5), Span(0, 40, 10)]);

        spans.Select(span => span.OwnMs).Should().Equal(70, 15, 5, 10);
        spans.Select(span => span.Depth).Should().Equal(0, 1, 2, 1);
    }

    // A concurrent span (a request) runs next to its siblings: a sum of durations would give a negative own time.
    [Fact]
    public void OverlappingChildren_CountOnce()
    {
        var spans = TelemetryStartupAnalysis.Of([Span(-1, 0, 100), Span(0, 10, 50), Span(0, 20, 50), Span(0, 90, 50)]);

        spans[0].OwnMs.Should().Be(30);
    }

    [Fact]
    public void ADepthOfTheTrace_CountsFromOne()
    {
        TelemetryStartupAnalysis.Depth([]).Should().Be(0);
        TelemetryStartupAnalysis.Depth([Span(-1, 0, 1), Span(0, 0, 1)]).Should().Be(2);
    }
}
