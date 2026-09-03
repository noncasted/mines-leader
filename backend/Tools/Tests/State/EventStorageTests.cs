using Common;
using FluentAssertions;
using Infrastructure.State;
using Marten;
using Shared;
using Tests.Fixtures;
using Tests.Grains;
using Xunit;

namespace Tests.State;

[Collection(nameof(OrleansIntegrationCollection))]
public class EventStorageTests
    (OrleansTestClusterFixture fixture) : IntegrationTestBase<OrleansTestClusterFixture>(fixture)
{
    [Fact]
    public async Task Delete_RemovesStream()
    {
        var eventStorage = GetSiloService<IEventStorage>();
        var store = GetSiloService<IDocumentStore>();
        var streamId = $"event_test:{Guid.NewGuid():D}";

        await eventStorage.Append(streamId, new CounterIncremented { Amount = 5 });

        var before = await store.QuerySession().Events.FetchStreamAsync(streamId, token: TestContext.Current.CancellationToken);
        before.Should().NotBeEmpty();

        await eventStorage.Delete(streamId);

        var after = await store.QuerySession().Events.FetchStreamAsync(streamId, token: TestContext.Current.CancellationToken);
        after.Should().BeEmpty();
    }

    [Fact]
    public async Task ReadBatch_MultipleStreams_ReturnsAll()
    {
        var eventStorage = GetSiloService<IEventStorage>();
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var streamId1 = $"event_test:{id1:D}";
        var streamId2 = $"event_test:{id2:D}";

        await eventStorage.Append(streamId1, new CounterIncremented { Amount = 10 });
        await eventStorage.Append(streamId2, new CounterIncremented { Amount = 20 });

        var result = await eventStorage.ReadBatch<Guid, EventTestAggregate>([streamId1, streamId2]);

        result.Should().HaveCount(2);
        result[id1].Counter.Should().Be(10);
        result[id2].Counter.Should().Be(20);
    }

    [Fact]
    public async Task ReadBatch_PartialMiss_ReturnsExisting()
    {
        var eventStorage = GetSiloService<IEventStorage>();
        var id = Guid.NewGuid();
        var streamId = $"event_test:{id:D}";

        await eventStorage.Append(streamId, new CounterIncremented { Amount = 7 });

        var result = await eventStorage.ReadBatch<Guid, EventTestAggregate>([streamId, $"event_test:{Guid.NewGuid():D}"]);

        result.Should().ContainKey(id);
        result[id].Counter.Should().Be(7);
    }

    [Fact]
    public async Task ReadBatch_EmptyList_ReturnsEmpty()
    {
        var eventStorage = GetSiloService<IEventStorage>();

        var result = await eventStorage.ReadBatch<Guid, EventTestAggregate>([]);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ReadAll_ReturnsAggregatesByPrefix()
    {
        var eventStorage = GetSiloService<IEventStorage>();
        var id = Guid.NewGuid();
        var streamId = $"event_test:{id:D}";

        await eventStorage.Append(streamId, new CounterIncremented { Amount = 3 });

        var results = new List<(Guid, EventTestAggregate)>();
        await foreach (var item in eventStorage.ReadAll<Guid, EventTestAggregate>("event_test:", GrainKeyType.Guid))
        {
            results.Add(item);
        }

        results.Should().Contain(r => r.Item1 == id && r.Item2.Counter == 3);
    }

    [Fact]
    public async Task ReadAll_EmptyPrefix_YieldsNothing()
    {
        var eventStorage = GetSiloService<IEventStorage>();

        var results = new List<(Guid, EventTestAggregate)>();
        await foreach (var item in eventStorage.ReadAll<Guid, EventTestAggregate>($"nonexistent_{Guid.NewGuid()}:", GrainKeyType.Guid))
        {
            results.Add(item);
        }

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task ReadPage_ReturnsOrderedPageAndTotalCount()
    {
        var eventStorage = GetSiloService<IEventStorage>();
        var prefix = $"page_test_{Guid.NewGuid():N}:";

        var amounts = new[] { 10, 30, 20 };
        foreach (var amount in amounts)
            await eventStorage.Append($"{prefix}{Guid.NewGuid():D}", new CounterIncremented { Amount = amount });

        var page = await eventStorage.ReadPage<Guid, EventTestAggregate>(
            prefix, GrainKeyType.Guid, offset: 0, limit: 2, orderByProperty: nameof(EventTestAggregate.Counter));

        page.TotalCount.Should().Be(3);
        page.Entries.Select(e => e.Value.Counter).Should().Equal(30, 20);

        var second = await eventStorage.ReadPage<Guid, EventTestAggregate>(
            prefix, GrainKeyType.Guid, offset: 2, limit: 2, orderByProperty: nameof(EventTestAggregate.Counter));

        second.TotalCount.Should().Be(3);
        second.Entries.Select(e => e.Value.Counter).Should().Equal(10);
    }

    [Fact]
    public async Task ReadPage_UnknownOrderProperty_FallsBackToId()
    {
        var eventStorage = GetSiloService<IEventStorage>();
        var prefix = $"page_test_{Guid.NewGuid():N}:";

        await eventStorage.Append($"{prefix}{Guid.NewGuid():D}", new CounterIncremented { Amount = 1 });

        var page = await eventStorage.ReadPage<Guid, EventTestAggregate>(
            prefix, GrainKeyType.Guid, offset: 0, limit: 10, orderByProperty: "NotAProperty");

        page.TotalCount.Should().Be(1);
        page.Entries.Should().HaveCount(1);
    }

    [Fact]
    public async Task ReadPage_EmptyPrefix_ReturnsEmpty()
    {
        var eventStorage = GetSiloService<IEventStorage>();

        var page = await eventStorage.ReadPage<Guid, EventTestAggregate>(
            $"nonexistent_{Guid.NewGuid():N}:", GrainKeyType.Guid, offset: 0, limit: 10);

        page.TotalCount.Should().Be(0);
        page.Entries.Should().BeEmpty();
    }
}
