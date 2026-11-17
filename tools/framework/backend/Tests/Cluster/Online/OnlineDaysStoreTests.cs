using Cluster.Monitoring.Online;
using FluentAssertions;
using Framework.Tests.Fixtures;
using Infrastructure;
using Xunit;

namespace Framework.Tests.Cluster.Online;

// The rows of the days through the real storage: what a restarted coordinator reads back.
[Collection(nameof(OrleansIntegrationCollection))]
public class OnlineDaysStoreTests(OrleansTestClusterFixture fixture)
    : IntegrationTestBase<OrleansTestClusterFixture>(fixture)
{
    private OnlineDaysStore Store => new(GetSiloService<IOrleans>());

    [Fact]
    public async Task WrittenDays_ReadBackInRange()
    {
        var day = new DateTime(2026, 11, 9, 0, 0, 0, DateTimeKind.Utc).AddDays(Random.Shared.Next(1000, 100000));
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        var counter = new OnlineCounter([]);
        counter.Apply(new HashSet<Guid> { a, b }, day.AddHours(10));
        counter.Apply(new HashSet<Guid> { a }, day.AddHours(11));
        counter.Apply(new HashSet<Guid> { b }, day.AddDays(1).AddHours(1));

        await Store.Write(counter.TakeChanged());

        var first = OnlineCounter.DayOf(day);
        var read = await Store.Read(first - 1, first + 1);

        read.Select(entry => entry.Day).Should().Equal(first, first + 1);
        read[0].Peak.Should().Be(2);
        read[0].Users.Should().BeEquivalentTo([a, b]);
        read[0].FindHour(10)!.UniqueUsers.Should().Be(2);
        read[0].FindHour(10)!.IsClosed.Should().BeTrue();
        read[0].FindHour(11)!.IsClosed.Should().BeTrue();
        read[1].FindHour(1)!.Users.Should().BeEquivalentTo([b]);
        read[1].FindHour(1)!.IsClosed.Should().BeFalse();
    }

    [Fact]
    public async Task RestartInSameHour_SameUserNotCountedAgain()
    {
        var hour = new DateTime(2026, 11, 9, 14, 0, 0, DateTimeKind.Utc).AddDays(Random.Shared.Next(1000, 100000));
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        var before = new OnlineCounter([]);
        before.Apply(new HashSet<Guid> { a, b }, hour);
        await Store.Write(before.TakeChanged());

        var now = hour.AddMinutes(30);
        var after = new OnlineCounter(await Store.Read(OnlineCounter.FirstKeptDay(now), OnlineCounter.DayOf(now)));
        after.Apply(new HashSet<Guid> { a }, now);

        var summary = after.Summary(now);
        summary.Hours[^1].UniqueUsers.Should().Be(2);
        summary.Hours[^1].Peak.Should().Be(2);
        summary.Days[^1].UniqueUsers.Should().Be(2);
        summary.Current.Should().Be(1);
    }

    // Two coordinators overlap for a while during a deploy: each writes the day from its own counter, and neither
    // write may drop the users or the peak the other has stored.
    [Fact]
    public async Task OverlappingCoordinators_WritesOfTheSameDay_AreMerged()
    {
        var hour = new DateTime(2026, 11, 9, 14, 0, 0, DateTimeKind.Utc).AddDays(Random.Shared.Next(1000, 100000));
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var c = Guid.NewGuid();

        var old = new OnlineCounter([]);
        var replacing = new OnlineCounter([]);

        old.Apply(new HashSet<Guid> { a, b }, hour.AddMinutes(1));
        replacing.Apply(new HashSet<Guid> { c }, hour.AddMinutes(2));

        await Store.Write(replacing.TakeChanged());
        await Store.Write(old.TakeChanged());

        replacing.Apply(new HashSet<Guid>(), hour.AddMinutes(3));
        await Store.Write(replacing.TakeChanged());

        var day = OnlineCounter.DayOf(hour);
        var read = await Store.Read(day, day);

        read.Should().ContainSingle();
        read[0].Users.Should().BeEquivalentTo([a, b, c]);
        read[0].Peak.Should().Be(2);
        read[0].FindHour(14)!.Users.Should().BeEquivalentTo([a, b, c]);
        read[0].FindHour(14)!.UniqueUsers.Should().Be(3);
        read[0].FindHour(14)!.Peak.Should().Be(2);
    }
}
