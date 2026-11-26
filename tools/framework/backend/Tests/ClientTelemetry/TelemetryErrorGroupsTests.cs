using FluentAssertions;
using Game.ClientTelemetry;
using Infrastructure.State;
using Newtonsoft.Json;
using Xunit;

namespace Framework.Tests.ClientTelemetry;

public class TelemetryErrorGroupsTests
{
    private static readonly DateTime _start = new(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);

    [Fact]
    public void ASession_CountsTheRepeats_FromTheMostFrequent()
    {
        var groups = TelemetrySessionErrorGroups.Of(
        [
            Row("rare", 1, 0),
            Row("often", 5, 1),
            Row("rare", 2, 2),
            Row("often", 4, 3)
        ]);

        groups.OmittedGroups.Should().Be(0);
        groups.OmittedCount.Should().Be(0);
        groups.Groups.Select(group => group.Message).Should().Equal("often", "rare");

        var often = groups.Groups[0];
        often.Count.Should().Be(9);
        often.Rows.Should().Be(2);
        often.FirstSeenAt.Should().Be(_start.AddSeconds(1));
        often.LastSeenAt.Should().Be(_start.AddSeconds(3));
        often.Level.Should().Be(TelemetryErrorLevel.Error);
        often.Fingerprint.Should().Be(Fingerprint("often"));
    }

    [Fact]
    public void ASession_OfEqualCounts_GoesFromTheFreshest()
    {
        var groups = TelemetrySessionErrorGroups.Of([Row("old", 3, 0), Row("fresh", 3, 1)]);

        groups.Groups.Select(group => group.Message).Should().Equal("fresh", "old");
    }

    [Fact]
    public void OverTheLimit_TheRarestAreCountedOnly()
    {
        var rows = Enumerable.Range(0, TelemetrySessionErrorGroups.MaxGroups + 3)
            .Select(index => Row($"e{index}", index + 1, index))
            .ToArray();

        var groups = TelemetrySessionErrorGroups.Of(rows);

        groups.Groups.Should().HaveCount(TelemetrySessionErrorGroups.MaxGroups);
        groups.OmittedGroups.Should().Be(3);
        groups.OmittedCount.Should().Be(1 + 2 + 3);
        groups.Groups[^1].Message.Should().Be("e3");
    }

    [Fact]
    public void TheMessageOfAGroup_IsCutToTheLimit_TheFingerprintIsOfTheWhole()
    {
        var message = new string('x', 2000);

        var group = TelemetrySessionErrorGroups.Of([Row(message, 1, 0)]).Groups.Single();

        group.Message.Should().HaveLength(TelemetrySessionErrorGroups.MaxMessageLength);
        group.Fingerprint.Should().Be(Fingerprint(message));
    }

    // The groups stay in the memory of every host, for each session of the retention.
    [Fact]
    public void AGroupsOfTheWorstSession_StayCompact()
    {
        var rows = Enumerable.Range(0, TelemetrySessionErrorGroups.MaxGroups + 10)
            .Select(index => Row(new string((char)('a' + index), 2000), 1000, index))
            .ToArray();

        var json = JsonConvert.SerializeObject(TelemetrySessionErrorGroups.Of(rows), JsonStateSettings.CreateBase());

        json.Length.Should().BeLessThan(5 * 1024);
    }

    // The repeat comes from a public endpoint up to int.MaxValue: a checked sum in int would fail the close of the session for good.
    [Fact]
    public void HugeRepeats_DoNotBreakTheDigest()
    {
        var buffer = TelemetrySessionBuffer.Empty(new ClientTelemetryStorageOptions());

        buffer.Apply(new TelemetryBatch
        {
            Names = [],
            NameSlots = 0,
            Samples = [],
            Errors = [new TelemetryError(1, TelemetryErrorLevel.Error, "a", "", int.MaxValue, null), new TelemetryError(2, TelemetryErrorLevel.Error, "a", "", int.MaxValue, null)],
            Marks = []
        }, _start);

        var digest = TelemetrySummaryCalculator.Digest(buffer);

        digest.ErrorCount.Should().Be(2L * int.MaxValue);
        digest.ErrorGroups.Groups.Single().Count.Should().Be(2L * int.MaxValue);
    }

    [Fact]
    public void TheDigest_HoldsTheGroups_ThroughTheJsonOfTheStates()
    {
        var buffer = TelemetrySessionBuffer.Empty(new ClientTelemetryStorageOptions());

        buffer.Apply(new TelemetryBatch
        {
            Names = [],
            NameSlots = 0,
            Samples = [],
            Errors = [new TelemetryError(1, TelemetryErrorLevel.Exception, "a", "A:B ()", 3, null)],
            Marks = []
        }, _start);

        var state = new TelemetrySessionState();
        state.Open(Guid.NewGuid(), new TelemetryDevice(), [], null, null, _start);
        state.Close(TelemetrySessionStatus.Ended, _start, buffer);

        var settings = JsonStateSettings.CreateBase();
        var restored = JsonConvert.DeserializeObject<TelemetrySessionState>(JsonConvert.SerializeObject(state, settings), settings)!;

        var group = restored.Digest!.ErrorGroups.Groups.Single();
        group.Should().Be(state.Digest!.ErrorGroups.Groups.Single());
        group.Level.Should().Be(TelemetryErrorLevel.Exception);
        group.Count.Should().Be(3);
    }

    [Fact]
    public void GlobalGroups_GoByFrequency_ThenByFreshness()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        var result = TelemetryGlobalErrorGroups.Of(
        [
            Closed(first, Row("seldom", 2, 0), Row("older", 5, 1), Row("newer", 2, 2)),
            Closed(second, Row("newer", 3, 3), Row("frequent", 9, 2))
        ], 100);

        result.TotalGroups.Should().Be(4);
        result.Groups.Select(group => group.Message).Should().Equal("frequent", "newer", "older", "seldom");

        var newer = result.Groups[1];
        newer.Count.Should().Be(5);
        newer.Sessions.Should().Be(2);
        newer.SessionIds.Should().Equal(second, first);
        newer.FirstSeenAt.Should().Be(_start.AddSeconds(2));
        newer.LastSeenAt.Should().Be(_start.AddSeconds(3));
    }

    [Fact]
    public void GlobalGroups_AreCutByTheLimit_AndTheTotalStays()
    {
        var result = TelemetryGlobalErrorGroups.Of([Closed(Guid.NewGuid(), Row("a", 3, 0), Row("b", 2, 1), Row("c", 1, 2))], 2);

        result.TotalGroups.Should().Be(3);
        result.Groups.Select(group => group.Message).Should().Equal("a", "b");
    }

    [Fact]
    public void TheLinksToSessions_AreBounded_TheFreshestFirst_AndTheNumberIsWhole()
    {
        var sessions = Enumerable.Range(0, TelemetryGlobalErrorGroups.MaxSessionIds + 5)
            .Select(index => Closed(Guid.NewGuid(), Row("a", 1, index)))
            .ToArray();

        var group = TelemetryGlobalErrorGroups.Of(sessions, 10).Groups.Single();

        group.Sessions.Should().Be(sessions.Length);
        group.Count.Should().Be(sessions.Length);
        group.SessionIds.Should().HaveCount(TelemetryGlobalErrorGroups.MaxSessionIds);
        group.SessionIds[0].Should().Be(sessions[^1].Id);
    }

    [Fact]
    public void WhatIsNotCovered_IsCounted()
    {
        var rows = Enumerable.Range(0, TelemetrySessionErrorGroups.MaxGroups + 2).Select(index => Row($"e{index}", index + 1, index)).ToArray();
        var withDropped = Closed(Guid.NewGuid(), rows);

        withDropped = withDropped with { Digest = withDropped.Digest! with { DroppedErrors = 7 } };

        var result = TelemetryGlobalErrorGroups.Of([withDropped, Live(Guid.NewGuid()), Closed(Guid.NewGuid(), Row("e0", 1, 0))], 100);

        result.CountedSessions.Should().Be(2);
        result.SessionsWithoutDigest.Should().Be(1);
        result.OmittedGroups.Should().Be(2);
        result.OmittedCount.Should().Be(1 + 2);
        result.DroppedErrors.Should().Be(7);
    }

    private static TelemetryStoredError Row(string message, int repeat, int second)
    {
        return new TelemetryStoredError(1, second * 1000L, TelemetryErrorLevel.Error, message, "", repeat, _start.AddSeconds(second), Fingerprint(message));
    }

    private static string Fingerprint(string message)
    {
        return TelemetryErrorFingerprint.Of(TelemetryErrorLevel.Error, message, "", null);
    }

    private static TelemetrySessionInfo Closed(Guid id, params TelemetryStoredError[] rows)
    {
        return Live(id) with
        {
            Status = TelemetrySessionStatus.Ended,
            Digest = new TelemetrySessionDigest(0, rows.Length, rows.Sum(row => (long)row.Repeat), 0, 0, 0, 0, 0, new TelemetrySessionSummary(0, 0, []), TelemetrySessionErrorGroups.Of(rows))
        };
    }

    private static TelemetrySessionInfo Live(Guid id)
    {
        return new TelemetrySessionInfo
        {
            Id = id,
            Status = TelemetrySessionStatus.Live,
            Device = new TelemetryDevice(),
            StartedAt = _start,
            LastSampleAt = null,
            LastSeq = 1,
            Names = [],
            NameSlots = 0
        };
    }
}
