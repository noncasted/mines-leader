using Console.Infrastructure.ClientTelemetry;
using Game.ClientTelemetry;
using Xunit;
using static Framework.Tests.ClientTelemetry.TelemetryConsoleSamples;

namespace Framework.Tests.ClientTelemetry;

public class TelemetryConsoleListTests
{
    [Fact]
    public void Filters_ByStatusAndPlatform_KeepAllPlatformsInTheChoice()
    {
        TelemetrySessionInfo[] all =
        [
            SessionOf(TelemetrySessionStatus.Live, "web"),
            SessionOf(TelemetrySessionStatus.Ended, "web"),
            SessionOf(TelemetrySessionStatus.Ended, "steam"),
            SessionOf(TelemetrySessionStatus.Lost, null)
        ];

        var page = TelemetrySessionListPage.Of(all, new TelemetrySessionQuery(TelemetrySessionStatus.Ended, "web", null, 0), 10);

        Assert.Equal([all[1]], page.Rows);
        Assert.Equal(["steam", "web"], page.Platforms);
    }

    [Fact]
    public void Filters_ByTag_TheChoiceComesFromTheData_OfAllSessions()
    {
        TelemetrySessionInfo[] all =
        [
            SessionOf(tags: ["beta"]),
            SessionOf(tags: ["alpha", "beta"]),
            SessionOf(tags: ["gamma"]),
            SessionOf()
        ];

        var page = TelemetrySessionListPage.Of(all, new TelemetrySessionQuery(null, null, "beta", 0), 10);

        Assert.Equal([all[0], all[1]], page.Rows);
        Assert.Equal(["alpha", "beta", "gamma"], page.Tags);
    }

    [Fact]
    public void Filter_NoTagChosen_TakesTheSessionsWithoutTagsToo()
    {
        TelemetrySessionInfo[] all = [SessionOf(tags: ["alpha"]), SessionOf()];

        var page = TelemetrySessionListPage.Of(all, new TelemetrySessionQuery(null, null, null, 0), 10);

        Assert.Equal(all, page.Rows);
    }

    [Fact]
    public void Tag_IsAnExactMatch_AndTheTotalsFollowIt()
    {
        TelemetrySessionInfo[] all =
        [
            SessionOf(TelemetrySessionStatus.Live, tags: ["alpha"]),
            SessionOf(tags: ["alpha.long"]),
            SessionOf(TelemetrySessionStatus.Lost, tags: ["beta"])
        ];

        var page = TelemetrySessionListPage.Of(all, new TelemetrySessionQuery(null, null, "alpha", 0), 10);

        Assert.Equal([all[0]], page.Rows);
        Assert.Equal(new TelemetrySessionTotals(1, 0, 0, 0, 0, 0, 0, 0), page.Totals);
    }

    [Fact]
    public void Page_OutOfTheMatching_GoesToTheLastOne()
    {
        var all = Enumerable.Range(0, 5).Select(_ => SessionOf()).ToArray();

        var page = TelemetrySessionListPage.Of(all, new TelemetrySessionQuery(null, null, null, 7), 2);

        Assert.Equal(2, page.Page);
        Assert.Equal(3, page.TotalPages);
        Assert.Equal([all[4]], page.Rows);
    }

    [Fact]
    public void Page_OfNothing_IsTheFirstOfOne()
    {
        var page = TelemetrySessionListPage.Of([], new TelemetrySessionQuery(null, null, null, 3), 2);

        Assert.Equal(0, page.Page);
        Assert.Equal(1, page.TotalPages);
        Assert.Empty(page.Rows);
    }

    [Fact]
    public void Totals_CountTheMatchingOnes_ALiveOneHasNoCounters()
    {
        TelemetrySessionInfo[] all =
        [
            SessionOf(TelemetrySessionStatus.Live),
            SessionOf(TelemetrySessionStatus.Ended, digest: Digest(errorCount: 3, droppedSamples: 1, droppedErrors: 2, droppedMarks: 4, rejectedRequests: 5)),
            SessionOf(TelemetrySessionStatus.Lost, digest: Digest(errorCount: 10, rejectedRequests: 1)),
            SessionOf(TelemetrySessionStatus.Ended, "steam", Digest(errorCount: 100))
        ];

        var totals = TelemetrySessionListPage.Of(all, new TelemetrySessionQuery(null, "web", null, 0), 10).Totals;

        Assert.Equal(new TelemetrySessionTotals(1, 1, 1, 13, 1, 2, 4, 6), totals);
    }

    [Fact]
    public void Duration_OfDigest_ElseUpToTheLastSample()
    {
        var started = SessionOf().StartedAt;

        Assert.Equal(TimeSpan.FromSeconds(42), TelemetrySessionFacts.Duration(SessionOf(digest: Digest(durationMs: 42000), lastSampleAt: started.AddHours(1))));
        Assert.Equal(TimeSpan.FromSeconds(9), TelemetrySessionFacts.Duration(SessionOf(TelemetrySessionStatus.Live, lastSampleAt: started.AddSeconds(9))));
        Assert.Equal(TimeSpan.Zero, TelemetrySessionFacts.Duration(SessionOf(TelemetrySessionStatus.Live)));
    }

    [Fact]
    public void AverageFps_OnlyFromTheSummaryWithTheMetric()
    {
        Assert.Equal(58.5, TelemetrySessionFacts.AverageFps(SessionOf(digest: Digest(metrics: [Metric("memory.allocated", 1), Metric("frame.fps", 58.5)]))));
        Assert.Null(TelemetrySessionFacts.AverageFps(SessionOf(digest: Digest(metrics: Metric("memory.allocated", 1)))));
        Assert.Null(TelemetrySessionFacts.AverageFps(SessionOf(TelemetrySessionStatus.Live)));
    }

    [Fact]
    public void Device_ListsOnlyWhatIsKnown()
    {
        var full = new TelemetryDevice
        {
            Platform = "web", Browser = "chrome", BrowserMajor = 120, Os = "windows", OsMajor = 11, Cores = 8, ScreenWidth = 1920, ScreenHeight = 1080
        };

        Assert.Equal("web · chrome 120 · windows 11 · 8 cores · 1920x1080", TelemetrySessionFacts.Device(full));
        Assert.Equal("steam", TelemetrySessionFacts.Device(new TelemetryDevice { Platform = "steam" }));
        Assert.Equal("-", TelemetrySessionFacts.Device(new TelemetryDevice()));
    }

    [Theory]
    [InlineData(1536.0, "bytes", "1.5 KB")]
    [InlineData(157286400.0, "bytes", "150 MB")]
    [InlineData(-1048576.0, "bytes", "-1 MB")]
    [InlineData(16.666, "ms", "16.67 ms")]
    [InlineData(58.25, "count", "58.25")]
    [InlineData(1200000.0, null, "1,200,000")]
    public void Value_ByUnit(double value, string? unit, string expected)
    {
        Assert.Equal(expected, TelemetryValueFormat.Value(value, unit));
    }

    [Theory]
    [InlineData(0L, "0:00")]
    [InlineData(65_000L, "1:05")]
    [InlineData(3_725_000L, "1:02:05")]
    public void Time_FromTheStartOfTheSession(long ms, string expected)
    {
        Assert.Equal(expected, TelemetryValueFormat.Time(ms));
    }

    [Fact]
    public void ErrorGroups_ByFrequencyOrFreshness()
    {
        var now = SessionOf().StartedAt;
        TelemetryGlobalErrorGroup[] groups = [Group("old-often", 9, now), Group("fresh-rare", 1, now.AddHours(1)), Group("fresh-often", 9, now.AddHours(1))];

        Assert.Equal(["fresh-often", "old-often", "fresh-rare"], TelemetryErrorOrdering.Apply(groups, TelemetryErrorOrder.Frequency).Select(group => group.Fingerprint));
        Assert.Equal(["fresh-often", "fresh-rare", "old-often"], TelemetryErrorOrdering.Apply(groups, TelemetryErrorOrder.Freshness).Select(group => group.Fingerprint));
    }
}
