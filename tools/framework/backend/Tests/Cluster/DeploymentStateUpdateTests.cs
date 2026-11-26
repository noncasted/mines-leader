using Cluster.Deploy;
using Cluster.Monitoring;
using Framework.Tests.Fixtures;
using Internal;
using FluentAssertions;
using Infrastructure;
using Infrastructure.State;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Framework.Tests.Cluster;

// a channel update that does not deserialize fails loudly instead of leaving a silently stale value.
// updates from the channel and from the own writes arrive on different threads: they are applied one
// at a time and the newest one wins.
public class DeploymentStateUpdateTests : LifetimeTest
{
    [Fact]
    public async Task Update_DeserializationFails_Throws()
    {
        var orleans = Substitute.For<IOrleans>();
        var error = new FormatException("bad payload");

        orleans.StateStorage.Read<AddressableStateValue>(Arg.Any<StateIdentity>())
               .Returns(Task.FromResult(new AddressableStateValue()));
        orleans.Serializer.Deserialize<LiveSessionsData>(Arg.Any<string>()).Returns(_ => throw error);

        var initial = new LiveSessionsData();
        var state = new TestState(orleans, initial);
        await state.OnDeployChanged(Guid.NewGuid(), new Lifetime(TestLifetime));

        var update = new AddressableStateValue
        {
            IsInitialized = true,
            UpdateDate = DateTime.UtcNow,
            Value = "not a LiveSessionsData"
        };

        var act = () => state.Receive(update);

        act.Should().Throw<InvalidOperationException>().Which.InnerException.Should().BeSameAs(error);
        state.Value.Slices.Should().BeEmpty();
    }

    [Fact]
    public async Task ConcurrentUpdates_AppliedOneAtATime_NewestWins()
    {
        const int threads = 8;
        const int perThread = 300;

        var orleans = Substitute.For<IOrleans>();

        orleans.StateStorage.Read<AddressableStateValue>(Arg.Any<StateIdentity>())
               .Returns(Task.FromResult(new AddressableStateValue()));
        orleans.Serializer.Deserialize<Marker>(Arg.Any<string>())
               .Returns(call => new Marker { Id = call.Arg<string>() });

        var state = new MarkerState(orleans);
        var lifetime = new Lifetime(TestLifetime);
        await state.OnDeployChanged(Guid.NewGuid(), lifetime);

        var active = 0;
        var overlaps = 0;

        state.Advise(lifetime, (valueLifetime, _) => {
            if (Interlocked.Increment(ref active) > 1)
                Interlocked.Increment(ref overlaps);

            // Subscriptions of the value lifetime: the next Set terminates them while other threads apply.
            valueLifetime.Listen(() => { });
            Thread.SpinWait(100);

            Interlocked.Decrement(ref active);
        });

        var start = DateTime.UtcNow;
        var errors = new System.Collections.Concurrent.ConcurrentQueue<Exception>();
        using var gate = new ManualResetEventSlim(false);

        var workers = Enumerable.Range(0, threads).Select(thread => Task.Factory.StartNew(() => {
            gate.Wait();

            for (var i = 0; i < perThread; i++)
            {
                // Interleaved dates: every thread delivers both older and newer updates than the others.
                var index = i * threads + thread;

                try
                {
                    state.Receive(new AddressableStateValue
                    {
                        IsInitialized = true,
                        UpdateDate = start.AddTicks(index + 1),
                        Value = index.ToString()
                    });
                }
                catch (Exception e)
                {
                    errors.Enqueue(e);
                }
            }
        }, TaskCreationOptions.LongRunning)).ToArray();

        gate.Set();
        await Task.WhenAll(workers);

        errors.Should().BeEmpty();
        overlaps.Should().Be(0, "a value is applied only after the previous one is fully applied");
        state.Value.Id.Should().Be((threads * perThread - 1).ToString());

        lifetime.Terminate();
    }

    // The pages of the console subscribe and leave on the threads of their circuits while the updates apply.
    [Fact]
    public async Task ConcurrentSubscriptions_WhileUpdatesApply_LoseNothing()
    {
        var orleans = Substitute.For<IOrleans>();

        orleans.StateStorage.Read<AddressableStateValue>(Arg.Any<StateIdentity>())
               .Returns(Task.FromResult(new AddressableStateValue()));
        orleans.Serializer.Deserialize<Marker>(Arg.Any<string>())
               .Returns(call => new Marker { Id = call.Arg<string>() });

        var state = new MarkerState(orleans);
        var lifetime = new Lifetime(TestLifetime);
        await state.OnDeployChanged(Guid.NewGuid(), lifetime);
        var start = DateTime.UtcNow;

        await SubscriptionStress.Run<Marker>(
            (subscriber, listener) => state.View(subscriber, listener),
            index => state.Receive(new AddressableStateValue { IsInitialized = true, UpdateDate = start.AddTicks(index), Value = index.ToString() }),
            // A circuit that views before the first update gets the initial marker, without an id.
            marker => marker.Id == string.Empty ? 0 : int.Parse(marker.Id));

        lifetime.Terminate();
    }

    public class Marker
    {
        public string Id { get; init; } = string.Empty;
    }

    private class MarkerState(IOrleans orleans)
        : DeploymentState<Marker>(orleans, Substitute.For<IMessaging>(), NullLoggerFactory.Instance, new Marker())
    {
        public void Receive(AddressableStateValue state) => OnUpdate(state);
    }

    private class TestState(IOrleans orleans, LiveSessionsData baseValue)
        : DeploymentState<LiveSessionsData>(orleans, Substitute.For<IMessaging>(), NullLoggerFactory.Instance, baseValue)
    {
        public void Receive(AddressableStateValue state) => OnUpdate(state);
    }
}
