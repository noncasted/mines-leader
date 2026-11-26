using Cluster.Monitoring.Online;
using FluentAssertions;
using Xunit;

namespace Framework.Tests.Cluster.Online;

public class OnlineCounterTests
{
    private static readonly DateTime Noon = new(2026, 11, 9, 12, 10, 0, DateTimeKind.Utc);
    private static readonly Guid A = Guid.NewGuid();
    private static readonly Guid B = Guid.NewGuid();
    private static readonly Guid C = Guid.NewGuid();

    [Fact]
    public void SameUserTwice_CountedOnce()
    {
        // ConnectedUsersLiveData of two gateways (or two tabs) gives the same id twice: the tracker takes the set.
        var counter = new OnlineCounter([]);

        counter.Apply(Set(A, A, B), Noon);

        var summary = counter.Summary(Noon);
        summary.Current.Should().Be(2);
        CurrentHour(summary).Peak.Should().Be(2);
        CurrentHour(summary).UniqueUsers.Should().Be(2);
        CurrentDay(summary).UniqueUsers.Should().Be(2);
    }

    [Fact]
    public void ReturnInSameHour_NotCountedAgain()
    {
        var counter = new OnlineCounter([]);

        counter.Apply(Set(A), Noon);
        counter.Apply(Set(), Noon.AddMinutes(5));
        counter.Apply(Set(A), Noon.AddMinutes(10));

        var summary = counter.Summary(Noon.AddMinutes(10));
        CurrentHour(summary).UniqueUsers.Should().Be(1);
        CurrentDay(summary).UniqueUsers.Should().Be(1);
    }

    [Fact]
    public void ShortVisits_CountedAsUniqueWithPeakOfOverlap()
    {
        var counter = new OnlineCounter([]);

        counter.Apply(Set(A), Noon);
        counter.Apply(Set(A, B), Noon.AddMinutes(1));
        counter.Apply(Set(B), Noon.AddMinutes(2));
        counter.Apply(Set(C), Noon.AddMinutes(3));
        counter.Apply(Set(), Noon.AddMinutes(4));

        var summary = counter.Summary(Noon.AddMinutes(4));
        summary.Current.Should().Be(0);
        CurrentHour(summary).Peak.Should().Be(2);
        CurrentHour(summary).UniqueUsers.Should().Be(3);
        CurrentDay(summary).Peak.Should().Be(2);
    }

    [Fact]
    public void NextHour_StartsNewBucket_DayKeepsUnique()
    {
        var counter = new OnlineCounter([]);

        counter.Apply(Set(A, B), Noon);
        counter.Apply(Set(A), Noon.AddHours(1));

        var summary = counter.Summary(Noon.AddHours(1));
        summary.Hours.Should().HaveCount(OnlineCounter.HoursKept);
        summary.Hours[^2].Start.Should().Be(new DateTime(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc));
        summary.Hours[^2].Peak.Should().Be(2);
        summary.Hours[^2].UniqueUsers.Should().Be(2);
        CurrentHour(summary).Peak.Should().Be(1);
        CurrentHour(summary).UniqueUsers.Should().Be(1);
        CurrentDay(summary).UniqueUsers.Should().Be(2);
        CurrentDay(summary).Peak.Should().Be(2);
    }

    [Fact]
    public void NextDay_StartsNewDay_ClosesHoursOfPrevious()
    {
        var counter = new OnlineCounter([]);
        var lateEvening = new DateTime(2026, 11, 9, 23, 50, 0, DateTimeKind.Utc);

        counter.Apply(Set(A, B), lateEvening);
        counter.Apply(Set(A), lateEvening.AddMinutes(20));

        var changed = counter.TakeChanged();
        var previous = changed.Single(day => day.Day == OnlineCounter.DayOf(lateEvening));
        previous.HasOpenHours.Should().BeFalse();
        previous.Hours.Single().UniqueUsers.Should().Be(2);
        previous.Hours.Single().Users.Should().BeEmpty();

        var summary = counter.Summary(lateEvening.AddMinutes(20));
        summary.Days.Should().HaveCount(OnlineCounter.DaysKept);
        summary.Days[^1].Start.Should().Be(new DateTime(2026, 11, 10, 0, 0, 0, DateTimeKind.Utc));
        summary.Days[^1].UniqueUsers.Should().Be(1);
        summary.Days[^2].UniqueUsers.Should().Be(2);
        summary.Hours[^2].Peak.Should().Be(2);
        summary.UniqueLast7Days.Should().Be(2);
    }

    [Fact]
    public void Restored_SameUserInSameHour_NotCountedAgain()
    {
        var before = new OnlineCounter([]);
        before.Apply(Set(A, B), Noon);
        var stored = before.TakeChanged();

        var after = new OnlineCounter(stored);
        after.Apply(Set(A), Noon.AddMinutes(30));

        var summary = after.Summary(Noon.AddMinutes(30));
        summary.Current.Should().Be(1);
        CurrentHour(summary).Peak.Should().Be(2);
        CurrentHour(summary).UniqueUsers.Should().Be(2);
        CurrentDay(summary).UniqueUsers.Should().Be(2);
    }

    [Fact]
    public void Restored_StaleOpenHour_ClosedOnFirstApply()
    {
        var before = new OnlineCounter([]);
        before.Apply(Set(A, B), Noon);
        var stored = before.TakeChanged();

        var after = new OnlineCounter(stored);
        after.Apply(Set(A), Noon.AddHours(3));

        var day = after.TakeChanged().Single();
        day.FindHour(12)!.IsClosed.Should().BeTrue();
        day.FindHour(12)!.UniqueUsers.Should().Be(2);
        day.FindHour(15)!.IsClosed.Should().BeFalse();
        day.Users.Should().BeEquivalentTo([A, B]);
    }

    [Fact]
    public void UniqueOverDays_SameUserOnSeveralDaysCountedOnce()
    {
        var counter = new OnlineCounter([]);

        counter.Apply(Set(A), Noon.AddDays(-10));
        counter.Apply(Set(A, B), Noon.AddDays(-5));
        counter.Apply(Set(A, C), Noon);

        var summary = counter.Summary(Noon);
        summary.UniqueLast7Days.Should().Be(3);
        summary.UniqueLast30Days.Should().Be(3);
        summary.Days.Sum(day => day.UniqueUsers).Should().Be(5);
    }

    [Fact]
    public void UniqueLast7Days_ExcludesOlderDays()
    {
        var counter = new OnlineCounter([]);

        counter.Apply(Set(B), Noon.AddDays(-7));
        counter.Apply(Set(A), Noon);

        var summary = counter.Summary(Noon);
        summary.UniqueLast7Days.Should().Be(1);
        summary.UniqueLast30Days.Should().Be(2);
    }

    [Fact]
    public void OldDays_LeaveTheStatistics()
    {
        var counter = new OnlineCounter([]);

        counter.Apply(Set(B), Noon.AddDays(-OnlineCounter.DaysKept));
        counter.TakeChanged();
        counter.Apply(Set(A), Noon);

        var summary = counter.Summary(Noon);
        summary.UniqueLast30Days.Should().Be(1);
        summary.Days.Sum(day => day.UniqueUsers).Should().Be(1);
    }

    [Fact]
    public void TakeChanged_ReturnsCopies_OnlyChangedSinceLastCall()
    {
        var counter = new OnlineCounter([]);

        counter.Apply(Set(A), Noon);
        var first = counter.TakeChanged().Single();

        counter.Apply(Set(A, B), Noon.AddMinutes(1));

        first.Users.Should().BeEquivalentTo([A]);
        counter.TakeChanged().Single().Users.Should().BeEquivalentTo([A, B]);
        counter.TakeChanged().Should().BeEmpty();
    }

    [Fact]
    public void MarkChanged_DaysOfFailedWrite_ReturnedAgain()
    {
        var counter = new OnlineCounter([]);

        counter.Apply(Set(A), Noon);
        var failed = counter.TakeChanged();

        counter.MarkChanged(failed.Select(day => day.Day));

        counter.TakeChanged().Single().Day.Should().Be(OnlineCounter.DayOf(Noon));
    }

    private static OnlinePeriod CurrentHour(OnlineStatsLiveData summary) => summary.Hours[^1];

    private static OnlinePeriod CurrentDay(OnlineStatsLiveData summary) => summary.Days[^1];

    private static HashSet<Guid> Set(params Guid[] ids) => ids.ToHashSet();
}
