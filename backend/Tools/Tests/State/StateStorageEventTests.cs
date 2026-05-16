using Common.Reactive;
using FluentAssertions;
using Infrastructure.State;
using Marten;
using Tests.Fixtures;
using Tests.Grains;
using Xunit;

namespace Tests.State;

[Collection(nameof(OrleansIntegrationCollection))]
public class StateStorageEventTests
    (OrleansTestClusterFixture fixture) : IntegrationTestBase<OrleansTestClusterFixture>(fixture)
{
    [Fact]
    public async Task ReadBatch_EventState_ReturnsAggregates()
    {
        var storage = GetSiloService<IStateStorage>();
        var eventStorage = GetSiloService<IEventStorage>();
        var id = Guid.NewGuid();
        var streamId = $"event_test:{id:D}";

        await eventStorage.Append(streamId, new CounterIncremented { Amount = 42 });

        var stateInfo = storage.Registry.Get<EventTestAggregate>();
        var identity = new StateIdentity
        {
            Key = id,
            Type = stateInfo.Name,
            TableName = stateInfo.TableName,
            Extension = null
        };

        var result = await storage.ReadBatch<Guid, EventTestAggregate>([identity]);

        result.Should().ContainKey(id);
        result[id].Counter.Should().Be(42);
    }

    [Fact]
    public async Task Delete_EventState_RemovesStream()
    {
        var storage = GetSiloService<IStateStorage>();
        var eventStorage = GetSiloService<IEventStorage>();
        var store = GetSiloService<IDocumentStore>();
        var id = Guid.NewGuid();
        var streamId = $"event_test:{id:D}";

        await eventStorage.Append(streamId, new CounterIncremented { Amount = 7 });

        var stateInfo = storage.Registry.Get<EventTestAggregate>();
        var identity = new StateIdentity
        {
            Key = id,
            Type = stateInfo.Name,
            TableName = stateInfo.TableName,
            Extension = null
        };

        await storage.Delete(identity);

        var events = await store.QuerySession().Events.FetchStreamAsync(streamId, token: TestContext.Current.CancellationToken);
        events.Should().BeEmpty();
    }

    [Fact]
    public async Task ReadAll_EventState_ReturnsAggregates()
    {
        var storage = GetSiloService<IStateStorage>();
        var eventStorage = GetSiloService<IEventStorage>();
        var id = Guid.NewGuid();
        var streamId = $"event_test:{id:D}";

        await eventStorage.Append(streamId, new CounterIncremented { Amount = 99 });

        var lifetime = new Lifetime();
        var results = new List<(Guid, EventTestAggregate)>();

        await foreach (var item in storage.ReadAll<Guid, EventTestAggregate>(lifetime))
        {
            results.Add(item);
        }

        lifetime.Terminate();

        results.Should().Contain(r => r.Item1 == id && r.Item2.Counter == 99);
    }

    [Fact]
    public async Task Write_EventState_ThrowsNotSupported()
    {
        var storage = GetSiloService<IStateStorage>();
        var stateInfo = storage.Registry.Get<EventTestAggregate>();
        var identity = new StateIdentity
        {
            Key = Guid.NewGuid(),
            Type = stateInfo.Name,
            TableName = stateInfo.TableName,
            Extension = null
        };

        var act = () => storage.Write(new StateWriteRequest
        {
            Records = new Dictionary<StateIdentity, IStateValue>
            {
                { identity, new EventTestAggregate() }
            }
        });

        await act.Should().ThrowAsync<NotSupportedException>();
    }
}
