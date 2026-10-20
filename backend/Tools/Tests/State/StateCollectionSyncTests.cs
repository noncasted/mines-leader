using System.Collections.Concurrent;
using Common.Reactive;
using FluentAssertions;
using Infrastructure;
using Infrastructure.State;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Tests.Fixtures;
using Tests.Grains;
using Xunit;

namespace Tests.State;

/// <summary>
/// F2.5: StateCollection subscribes before the load and buffers notifications, re-reads records from storage
/// (order independent of clocks), fails fast on a failed load and is registered once.
/// </summary>
public class StateCollectionSyncTests
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task UpdateDuringLoad_IsNotLost()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        var utils = new FakeUtils { BlockLoad = true };
        utils.Storage[a] = Value(a, "v1");

        var collection = new TestCollection(utils);
        var lifetime = new Lifetime();
        var setup = collection.OnLocalSetupCompleted(lifetime);

        // The snapshot is taken (a = v1); the records change and are notified before the snapshot is applied.
        await utils.LoadStarted.Task.WaitAsync(Wait);
        utils.Listener.Should().NotBeNull("the subscription must exist before the load");

        utils.Storage[a] = Value(a, "v2");
        utils.Storage[b] = Value(b, "new");

        var notifyA = utils.Notify(a);
        var notifyB = utils.Notify(b);

        // Buffered: the delivery does not wait for the load.
        notifyA.IsCompleted.Should().BeTrue();
        notifyB.IsCompleted.Should().BeTrue();
        collection.IsLoaded.Should().BeFalse();

        utils.ReleaseLoad();
        await setup.WaitAsync(Wait);

        collection.IsLoaded.Should().BeTrue();
        collection[a].Name.Should().Be("v2");
        collection[b].Name.Should().Be("new");

        lifetime.Terminate();
    }

    [Fact]
    public async Task Notifications_OutOfOrderAndDuplicated_EndAtStoredValue()
    {
        var key = Guid.NewGuid();
        var utils = new FakeUtils();
        var collection = new TestCollection(utils);
        var lifetime = new Lifetime();

        await collection.OnLocalSetupCompleted(lifetime);

        var updated = 0;
        collection.Updated.Advise(lifetime, () => Interlocked.Increment(ref updated));

        // The newest record is in storage; stale or repeated notifications cannot bring an older value back.
        utils.Storage[key] = Value(key, "v3");

        await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Task.Run(() => utils.Notify(key))));

        collection[key].Name.Should().Be("v3");
        updated.Should().Be(20);

        lifetime.Terminate();
    }

    [Fact]
    public async Task RecordRemovedFromStorage_IsRemovedOnNotification()
    {
        var key = Guid.NewGuid();
        var utils = new FakeUtils();
        utils.Storage[key] = Value(key, "v1");

        var collection = new TestCollection(utils);
        var lifetime = new Lifetime();
        await collection.OnLocalSetupCompleted(lifetime);

        collection.ContainsKey(key).Should().BeTrue();

        utils.Storage.TryRemove(key, out _);
        await utils.Notify(key);

        collection.ContainsKey(key).Should().BeFalse();

        lifetime.Terminate();
    }

    [Fact]
    public async Task ConcurrentReadsAndUpdates_DoNotThrow()
    {
        var utils = new FakeUtils();
        var collection = new TestCollection(utils);
        var lifetime = new Lifetime();
        await collection.OnLocalSetupCompleted(lifetime);

        var keys = Enumerable.Range(0, 200).Select(_ => Guid.NewGuid()).ToArray();
        using var stop = new CancellationTokenSource();

        var reader = Task.Run(() => {
            var reads = 0;

            while (stop.IsCancellationRequested == false)
            {
                foreach (var (key, value) in collection)
                    value.Id.Should().Be(key);

                reads++;
            }

            return reads;
        });

        await Task.WhenAll(keys.Select(key => Task.Run(async () => {
            utils.Storage[key] = Value(key, "x");
            await utils.Notify(key);
        })));

        stop.Cancel();
        (await reader.WaitAsync(Wait)).Should().BePositive();

        collection.Count.Should().Be(keys.Length);

        lifetime.Terminate();
    }

    [Fact]
    public async Task NotificationRefreshFailure_PropagatesToDelivery()
    {
        var utils = new FakeUtils();
        var collection = new TestCollection(utils);
        var lifetime = new Lifetime();
        await collection.OnLocalSetupCompleted(lifetime);

        utils.ReadFailure = new InvalidOperationException("db down");

        // The durable queue sees the exception and retries the side effect instead of losing the update.
        await utils.Invoking(u => u.Notify(Guid.NewGuid())).Should().ThrowAsync<InvalidOperationException>();

        lifetime.Terminate();
    }

    [Fact]
    public async Task PartialLoad_Throws_AndSetupFails()
    {
        var storage = Substitute.For<IStateStorage>();
        storage.ReadAll<Guid, CollectionTestState>(Arg.Any<IReadOnlyLifetime>()).Returns(FailingReadAll());

        var utils = CreateUtils(storage, typeof(CollectionTestState));
        var collection = new TestCollection(utils);
        var lifetime = new Lifetime();

        // Before the fix the error was logged and the collection started with one of the records.
        await collection.Invoking(c => c.OnLocalSetupCompleted(lifetime)).Should().ThrowAsync<InvalidOperationException>()
                        .WithMessage("read failed");

        collection.IsLoaded.Should().BeFalse();
        collection.Count.Should().Be(0);

        lifetime.Terminate();

        static async IAsyncEnumerable<(Guid, CollectionTestState)> FailingReadAll()
        {
            var id = Guid.NewGuid();
            yield return (id, Value(id, "first"));
            await Task.Yield();
            throw new InvalidOperationException("read failed");
        }
    }

    [Fact]
    public async Task StateTypeMismatch_Throws()
    {
        var utils = CreateUtils(Substitute.For<IStateStorage>(), typeof(SimpleTestState));

        await utils.Invoking(u => u.Load(new Lifetime())).Should().ThrowAsync<InvalidOperationException>()
                   .WithMessage("*Type mismatch*");
    }

    [Fact]
    public void AddStateCollection_RegistersOneLoadedInstance()
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Services.AddLogging();
        builder.Services.AddSingleton(Substitute.For<IGrainStatesRegistry>());
        builder.Services.AddSingleton(Substitute.For<IStateStorage>());
        builder.Services.AddSingleton(Substitute.For<IMessaging>());

        builder.AddStateCollection<TestCollection, Guid, CollectionTestState>();

        builder.Services.Should().NotContain(d => d.ServiceType == typeof(IStateCollection<,>),
            "an open generic registration creates a second collection that is never loaded");

        using var provider = builder.Services.BuildServiceProvider();

        var collection = provider.GetRequiredService<TestCollection>();
        var setups = provider.GetServices<ILocalSetupCompleted>().ToArray();

        setups.Should().ContainSingle().Which.Should().BeSameAs(collection);
        provider.GetRequiredService<IStateCollection<Guid, CollectionTestState>>().Should().BeSameAs(collection);

        builder.Invoking(b => b.AddStateCollection<TestCollection, Guid, CollectionTestState>())
               .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task SetupTwice_Throws()
    {
        var collection = new TestCollection(new FakeUtils());
        var lifetime = new Lifetime();

        await collection.OnLocalSetupCompleted(lifetime);
        await collection.Invoking(c => c.OnLocalSetupCompleted(lifetime)).Should().ThrowAsync<InvalidOperationException>();

        lifetime.Terminate();
    }

    private static CollectionTestState Value(Guid id, string name)
    {
        return new CollectionTestState { Id = id, Name = name };
    }

    private static StateCollectionUtils<Guid, CollectionTestState> CreateUtils(IStateStorage storage, Type registeredType)
    {
        var registry = Substitute.For<IGrainStatesRegistry>();

        registry.Get<CollectionTestState>().Returns(new GrainStateInfo
        {
            TableName = "state_test_collection",
            KeyType = Shared.GrainKeyType.Guid,
            Type = registeredType,
            Name = "collection_test"
        });

        return new StateCollectionUtils<Guid, CollectionTestState>(registry,
            storage,
            Substitute.For<IMessaging>(),
            NullLogger<StateCollectionUtils<Guid, CollectionTestState>>.Instance);
    }

    public class TestCollection(StateCollectionUtils<Guid, CollectionTestState> utils)
        : StateCollection<Guid, CollectionTestState>(utils, NullLogger<StateCollection<Guid, CollectionTestState>>.Instance)
    {
        // DI constructor for AddStateCollection_RegistersOneLoadedInstance.
        [ActivatorUtilitiesConstructor]
        public TestCollection(StateCollectionUtils<Guid, CollectionTestState> utils, ILogger<TestCollection> logger)
            : this(utils)
        {
        }
    }

    // Storage and transport in memory: Storage is what the database holds, Notify is a durable queue delivery.
    private class FakeUtils() : StateCollectionUtils<Guid, CollectionTestState>(null!, null!, null!,
        NullLogger<StateCollectionUtils<Guid, CollectionTestState>>.Instance)
    {
        private readonly TaskCompletionSource _loadGate = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ConcurrentDictionary<Guid, CollectionTestState> Storage { get; } = new();
        public TaskCompletionSource LoadStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Func<Guid, Task>? Listener { get; private set; }
        public bool BlockLoad { get; init; }
        public Exception? ReadFailure { get; set; }

        public void ReleaseLoad()
        {
            _loadGate.TrySetResult();
        }

        public Task Notify(Guid key)
        {
            return Listener!(key);
        }

        public override async Task<IReadOnlyDictionary<Guid, CollectionTestState>> Load(IReadOnlyLifetime lifetime)
        {
            var snapshot = new Dictionary<Guid, CollectionTestState>(Storage);
            LoadStarted.TrySetResult();

            if (BlockLoad == true)
                await _loadGate.Task;

            return snapshot;
        }

        public override Task<IReadOnlyDictionary<Guid, CollectionTestState>> Read(IReadOnlyList<Guid> keys)
        {
            if (ReadFailure != null)
                throw ReadFailure;

            IReadOnlyDictionary<Guid, CollectionTestState> result = keys
                .Where(key => Storage.ContainsKey(key))
                .ToDictionary(key => key, key => Storage[key]);

            return Task.FromResult(result);
        }

        public override Task ListenUpdates(IReadOnlyLifetime lifetime, Func<Guid, Task> onUpdate)
        {
            Listener = onUpdate;
            return Task.CompletedTask;
        }
    }
}

/// <summary>
/// F2.5: StateCollectionUtils.Read against the real database - the re-read used for every notification.
/// </summary>
[Collection(nameof(OrleansIntegrationCollection))]
public class StateCollectionReadTests
    (OrleansTestClusterFixture fixture) : IntegrationTestBase<OrleansTestClusterFixture>(fixture)
{
    [Fact]
    public async Task Read_ReturnsStoredRecords_MissingKeysAbsent()
    {
        var stored = Guid.NewGuid();
        var missing = Guid.NewGuid();

        await GetGrain<ICollectionTestGrain>(stored).SetName("stored");

        var utils = GetSiloService<StateCollectionUtils<Guid, CollectionTestState>>();
        var result = await utils.Read([stored, missing]);

        result.Should().ContainKey(stored);
        result[stored].Name.Should().Be("stored");
        result.Should().NotContainKey(missing);
    }
}
