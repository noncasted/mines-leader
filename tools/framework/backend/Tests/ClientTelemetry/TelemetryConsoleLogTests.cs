using Console.Infrastructure.ClientTelemetry;
using Game.ClientTelemetry;
using Xunit;
using static Framework.Tests.ClientTelemetry.TelemetryConsoleSamples;

namespace Framework.Tests.ClientTelemetry;

public class TelemetryConsoleLogTests
{
    [Fact]
    public async Task Refresh_OfAnUnknownSession_FindsNothing()
    {
        var log = new TelemetrySessionLog(new FakeTelemetryReader(), Guid.NewGuid());

        Assert.False(await log.Refresh());
        Assert.Null(log.Info);
    }

    [Fact]
    public async Task Refresh_TakesAllPages_ThenOnlyTheNewRows()
    {
        var reader = new FakeTelemetryReader();
        var session = SessionOf(TelemetrySessionStatus.Live);
        reader.Set(session);
        reader.AddSamples(Enumerable.Range(1, TelemetrySessionLog.PageRows * 2 + 1).Select(i => Sample(i, i, 1)).ToArray());
        reader.AddMarks(Mark(1, 5, "m"));
        reader.SetErrors(session.Id, Error(1, 6, "a"));

        var log = new TelemetrySessionLog(reader, session.Id);

        Assert.True(await log.Refresh());
        Assert.Equal(TelemetrySessionLog.PageRows * 2 + 1, log.Samples.Count);
        Assert.Single(log.Marks);
        Assert.Single(log.Errors);
        Assert.False(await log.Refresh());

        reader.AddSamples(Sample(100_000, 100_000, 1));

        Assert.True(await log.Refresh());
        Assert.Equal(TelemetrySessionLog.PageRows * 2 + 2, log.Samples.Count);
    }

    // The samples of a closed session came before the read of the errors failed: the next refresh still gives them to the page.
    [Fact]
    public async Task Refresh_AfterAPartlyFailedOne_ReportsTheRowsItTookBefore()
    {
        var reader = new FakeTelemetryReader();
        var session = SessionOf(TelemetrySessionStatus.Ended);
        reader.Set(session);
        reader.AddSamples(Sample(1, 1000, 1), Sample(2, 2000, 2));
        reader.FailNextErrorRead(new InvalidOperationException("database down"));

        var log = new TelemetrySessionLog(reader, session.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(log.Refresh);

        Assert.True(await log.Refresh());
        Assert.Equal(2, log.Samples.Count);
        Assert.False(await log.Refresh());
    }

    [Fact]
    public async Task Samples_GoInTheOrderOfTime_WhateverTheOrderOfSeq()
    {
        var reader = new FakeTelemetryReader();
        var session = SessionOf(TelemetrySessionStatus.Live);
        reader.Set(session);
        reader.AddSamples(Sample(1, 2000, 1), Sample(2, 1000, 2));

        var log = new TelemetrySessionLog(reader, session.Id);
        await log.Refresh();

        Assert.Equal([1000, 2000], log.Samples.Select(sample => sample.Time));
    }

    [Fact]
    public async Task Summary_OfALiveSession_LeavesHiddenSamplesOut()
    {
        var reader = new FakeTelemetryReader();
        var session = SessionOf(TelemetrySessionStatus.Live);
        reader.Set(session);
        reader.AddSamples(Sample(1, 0, 60), Sample(1, 1000, 40), Sample(1, 2000, 1, hidden: true));

        var log = new TelemetrySessionLog(reader, session.Id);
        await log.Refresh();

        var summary = log.Summary;
        Assert.Equal(2, summary.Samples);
        Assert.Equal(1, summary.HiddenSamples);
        Assert.Equal(50, Assert.Single(summary.Metrics).Average);
    }

    [Fact]
    public async Task Summary_OfAClosedSession_IsItsDigest()
    {
        var reader = new FakeTelemetryReader();
        var digest = Digest(metrics: Metric("frame.fps", 33));
        var session = SessionOf(digest: digest);
        reader.Set(session);
        reader.AddSamples(Sample(1, 0, 60));

        var log = new TelemetrySessionLog(reader, session.Id);
        await log.Refresh();

        Assert.Same(digest.Summary, log.Summary);
    }

    [Fact]
    public async Task Refresh_SeesASessionThatHasClosed()
    {
        var reader = new FakeTelemetryReader();
        var session = SessionOf(TelemetrySessionStatus.Live);
        reader.Set(session);

        var log = new TelemetrySessionLog(reader, session.Id);
        await log.Refresh();
        Assert.True(log.IsLive);

        reader.Set(session with { Status = TelemetrySessionStatus.Lost });

        Assert.True(await log.Refresh());
        Assert.False(log.IsLive);
    }

    [Fact]
    public async Task ErrorStack_ComesFromTheFirstSessionThatStillHasARow()
    {
        var reader = new FakeTelemetryReader();
        Guid empty = Guid.NewGuid(), other = Guid.NewGuid(), holder = Guid.NewGuid(), unread = Guid.NewGuid();
        reader.SetErrors(other, Error(1, 0, "other"));
        reader.SetErrors(holder, Error(1, 0, "other"), Error(2, 0, "wanted"));
        reader.SetErrors(unread, Error(1, 0, "wanted"));

        var group = Group("wanted", 1, SessionOf().StartedAt, empty, other, holder, unread);
        var found = await TelemetryErrorExample.Find(reader, group);

        Assert.Equal(holder, found!.Session);
        Assert.Equal("wanted", found.Error.Fingerprint);
        Assert.DoesNotContain(unread, reader.ErrorReads);
    }

    [Fact]
    public async Task ErrorStack_LooksThroughThePagesOfASession()
    {
        var reader = new FakeTelemetryReader();
        var session = Guid.NewGuid();
        reader.SetErrors(session, Enumerable.Range(1, TelemetrySessionLog.PageRows + 1).Select(i => Error(i, 0, i == TelemetrySessionLog.PageRows + 1 ? "wanted" : "other")).ToArray());

        Assert.NotNull(await TelemetryErrorExample.Find(reader, Group("wanted", 1, SessionOf().StartedAt, session)));
    }

    [Fact]
    public async Task ErrorStack_WithoutARowInAnySession_IsNull()
    {
        var group = Group("wanted", 1, SessionOf().StartedAt, Guid.NewGuid());

        Assert.Null(await TelemetryErrorExample.Find(new FakeTelemetryReader(), group));
    }
}
