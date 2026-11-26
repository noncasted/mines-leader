using Common;
using FluentAssertions;
using Infrastructure.State;
using Marten;
using Newtonsoft.Json.Linq;
using Shared;
using Framework.Tests.Fixtures;
using Framework.Tests.Grains;
using Xunit;

namespace Framework.Tests.State;

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

    // Who writes Id: Marten, through the identity member of the snapshot; the framework does not. The second append goes
    // through the stored snapshot: an Id the serializer can't restore loses the first event of the snapshot silently.
    [Fact]
    public async Task EveryRead_IdIsTheStreamId_AfterSecondAppend()
    {
        var eventStorage = GetSiloService<IEventStorage>();
        var key = Guid.NewGuid();
        var streamId = $"event_test:{key:D}";

        await eventStorage.Append(streamId, new CounterIncremented { Amount = 3 });
        await eventStorage.Append(streamId, new CounterIncremented { Amount = 4 });

        var read = await eventStorage.Read<EventTestAggregate>(streamId);
        read.Id.Should().Be(streamId);
        read.Counter.Should().Be(7);

        var rebuilt = await eventStorage.Rebuild<EventTestAggregate>(streamId);
        rebuilt.Id.Should().Be(streamId);
        rebuilt.Counter.Should().Be(7);

        var batch = await eventStorage.ReadBatch<Guid, EventTestAggregate>([streamId]);
        batch[key].Id.Should().Be(streamId);
        batch[key].Counter.Should().Be(7);

        var page = await eventStorage.ReadPage<Guid, EventTestAggregate>(streamId, GrainKeyType.Guid, offset: 0, limit: 10);
        page.Entries.Should().ContainSingle();
        page.Entries[0].Key.Should().Be(key);
        page.Entries[0].Value.Id.Should().Be(streamId);
        page.Entries[0].Value.Counter.Should().Be(7);

        var all = new List<(Guid Key, EventTestAggregate Aggregate)>();

        await foreach (var entry in eventStorage.ReadAll<Guid, EventTestAggregate>(streamId, GrainKeyType.Guid))
            all.Add(entry);

        all.Should().ContainSingle();
        all[0].Key.Should().Be(key);
        all[0].Aggregate.Id.Should().Be(streamId);
        all[0].Aggregate.Counter.Should().Be(7);
    }

    [Fact]
    public async Task Snapshot_StoresTheStreamIdInItsJson()
    {
        var eventStorage = GetSiloService<IEventStorage>();
        var store = GetSiloService<IDocumentStore>();
        var streamId = $"event_test:{Guid.NewGuid():D}";

        await eventStorage.Append(streamId, new CounterIncremented { Amount = 1 });

        await using var session = store.QuerySession();
        var json = await session.Json.FindByIdAsync<EventTestAggregate>(streamId, TestContext.Current.CancellationToken);

        JObject.Parse(json!)["Id"]!.Value<string>().Should().Be(streamId);
    }

    [Fact]
    public async Task Read_MissingStream_IdIsEmpty()
    {
        var eventStorage = GetSiloService<IEventStorage>();

        var read = await eventStorage.Read<EventTestAggregate>($"event_test:{Guid.NewGuid():D}");

        read.Id.Should().BeEmpty();
    }

    [Fact]
    public async Task Read_MissingStream_IdNotRestorable_ReturnsNewAggregate()
    {
        var eventStorage = GetSiloService<IEventStorage>();

        var read = await eventStorage.Read<EventLostIdAggregate>($"event_lost_id_test:{Guid.NewGuid():D}");

        read.Id.Should().BeEmpty();
        read.Counter.Should().Be(0);
    }

    [Fact]
    public async Task Read_IdNotRestorable_Throws()
    {
        var eventStorage = GetSiloService<IEventStorage>();
        var streamId = await AppendLostIdStream(eventStorage, $"event_lost_id_test:{Guid.NewGuid():D}");

        var read = () => eventStorage.Read<EventLostIdAggregate>(streamId);

        await ShouldThrowLostId(read);
    }

    [Fact]
    public async Task ReadBatch_IdNotRestorable_Throws()
    {
        var eventStorage = GetSiloService<IEventStorage>();
        var streamId = await AppendLostIdStream(eventStorage, $"event_lost_id_test:{Guid.NewGuid():D}");

        var read = () => eventStorage.ReadBatch<string, EventLostIdAggregate>([streamId]);

        await ShouldThrowLostId(read);
    }

    [Fact]
    public async Task ReadAll_IdNotRestorable_Throws()
    {
        var eventStorage = GetSiloService<IEventStorage>();
        var streamId = await AppendLostIdStream(eventStorage, $"event_lost_id_test:{Guid.NewGuid():D}");

        var read = async () => {
            await foreach (var _ in eventStorage.ReadAll<string, EventLostIdAggregate>(streamId, GrainKeyType.String))
            {
            }
        };

        await ShouldThrowLostId(read);
    }

    [Fact]
    public async Task ReadPage_IdNotRestorable_Throws()
    {
        var eventStorage = GetSiloService<IEventStorage>();
        var streamId = await AppendLostIdStream(eventStorage, $"event_lost_id_test:{Guid.NewGuid():D}");

        var read = () => eventStorage.ReadPage<string, EventLostIdAggregate>(streamId, GrainKeyType.String, offset: 0, limit: 10);

        await ShouldThrowLostId(read);
    }

    // The way out of a stale snapshot: the events are whole and Marten sets the Id of the aggregate it builds.
    [Fact]
    public async Task Rebuild_IdNotRestorable_SeesEveryEvent()
    {
        var eventStorage = GetSiloService<IEventStorage>();
        var streamId = await AppendLostIdStream(eventStorage, $"event_lost_id_test:{Guid.NewGuid():D}");

        var rebuilt = await eventStorage.Rebuild<EventLostIdAggregate>(streamId);

        rebuilt.Id.Should().Be(streamId);
        rebuilt.Counter.Should().Be(7);
    }

    private static async Task<string> AppendLostIdStream(IEventStorage eventStorage, string streamId)
    {
        await eventStorage.Append(streamId, new LostIdCounterIncremented { Amount = 3 });
        await eventStorage.Append(streamId, new LostIdCounterIncremented { Amount = 4 });
        return streamId;
    }

    private static async Task ShouldThrowLostId(Func<Task> read)
    {
        var thrown = await read.Should().ThrowAsync<InvalidOperationException>();
        thrown.Which.Message.Should().Contain(nameof(EventLostIdAggregate)).And.Contain("[JsonProperty]");
    }

    [Fact]
    public async Task Grain_IdIsTheStreamId_AfterTransactionalAndStandaloneWrites_AndThroughOrleansCopy()
    {
        var transactional = Guid.NewGuid();
        var transactionalGrain = GetGrain<IEventTestGrain>(transactional);
        await RunTransaction(() => transactionalGrain.AppendEvents(2, "a"));

        var standalone = Guid.NewGuid();
        await GetSiloService<IEventStorage>().Append($"event_test:{standalone:D}", new CounterIncremented { Amount = 5 });

        var eventStorage = GetSiloService<IEventStorage>();
        (await eventStorage.Read<EventTestAggregate>($"event_test:{transactional:D}")).Id
            .Should().Be($"event_test:{transactional:D}");

        // The first read of this activation goes to the snapshot; the result comes back through the Orleans codec.
        var aggregate = await GetGrain<IEventTestGrain>(standalone).GetAggregate();
        aggregate.Id.Should().Be($"event_test:{standalone:D}");
        aggregate.Counter.Should().Be(5);
    }
}
