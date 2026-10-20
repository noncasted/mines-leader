using Cluster.Deploy;
using Cluster.Monitoring;
using Common.Reactive;
using FluentAssertions;
using Infrastructure;
using Infrastructure.State;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Tests.Cluster;

/// <summary>
/// F2.39: a channel update that does not deserialize fails loudly instead of leaving a silently stale value.
/// F2.40: updates from the channel and from the own writes arrive on different threads: they are applied one
/// at a time and the newest one wins.
/// </summary>
public class DeploymentStateUpdateTests
{
    [Fact]
    public async Task Update_DeserializationFails_Throws()
    {
        var orleans = Substitute.For<IOrleans>();
        var error = new FormatException("bad payload");

        orleans.StateStorage.Read<AddressableStateValue>(Arg.Any<StateIdentity>())
               .Returns(Task.FromResult(new AddressableStateValue()));
        orleans.Serializer.Deserialize<LiveMatchesData>(Arg.Any<string>()).Returns(_ => throw error);

        var initial = new LiveMatchesData();
        var state = new TestState(orleans, initial);
        await state.OnDeployChanged(Guid.NewGuid(), new Lifetime());

        var update = new AddressableStateValue
        {
            IsInitialized = true,
            UpdateDate = DateTime.UtcNow,
            Value = "not a LiveMatchesData"
        };

        var act = () => state.Receive(update);

        act.Should().Throw<InvalidOperationException>().Which.InnerException.Should().BeSameAs(error);
        state.Value.Matches.Should().BeEmpty();
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
        var lifetime = new Lifetime();
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

    public class Marker
    {
        public string Id { get; set; } = string.Empty;
    }

    private class MarkerState(IOrleans orleans)
        : DeploymentState<Marker>(orleans, Substitute.For<IMessaging>(), NullLoggerFactory.Instance, new Marker())
    {
        public void Receive(AddressableStateValue state) => OnUpdate(state);
    }

    private class TestState(IOrleans orleans, LiveMatchesData baseValue)
        : DeploymentState<LiveMatchesData>(orleans, Substitute.For<IMessaging>(), NullLoggerFactory.Instance, baseValue)
    {
        public void Receive(AddressableStateValue state) => OnUpdate(state);
    }
}
