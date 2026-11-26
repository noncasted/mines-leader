using Cluster.Deploy;
using FluentAssertions;
using Framework.Tests.Fixtures;
using Infrastructure;
using Infrastructure.State;
using Internal;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Framework.Tests.Cluster;

// A write of one deploy epoch that completes after the switch to the next epoch belongs to the old deploy row:
// it must neither become the value of the new epoch nor move its watermark.
public class DeploymentStateEpochTests : LifetimeTest
{
    public class Marker
    {
        public int Value { get; init; }
    }

    [Fact]
    public async Task SetValue_CompletesAfterDeploySwitch_NotAppliedToTheNewEpoch()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var write = new TaskCompletionSource();

        var storage = Substitute.For<IStateStorage>();
        storage.Registry.Returns(TestStates.CreateRegistry());
        storage.Read<AddressableStateValue>(Arg.Any<StateIdentity>()).Returns(Task.FromResult(new AddressableStateValue()));

        storage.Write(Arg.Is<StateWriteRequest>(request => IsOf(request, first))).Returns(write.Task);

        var orleans = Substitute.For<IOrleans>();
        orleans.StateStorage.Returns(storage);
        orleans.Serializer.Returns(new JsonTestSerializer());

        var messaging = Substitute.For<IMessaging>();

        var state = new TestState(orleans, messaging);
        var firstLifetime = new Lifetime(TestLifetime);
        await state.OnDeployChanged(first, firstLifetime);

        var pending = state.SetValue(new Marker { Value = 1 });

        firstLifetime.Terminate();
        await state.OnDeployChanged(second, new Lifetime(TestLifetime));

        write.SetResult();
        await pending;

        state.Value.Value.Should().Be(0, "the value written to the row of the previous deploy is not the value of this one");

        // The real value of the new epoch, stamped by a host with a clock behind, is not taken for stale.
        var stamped = DateTime.UtcNow.AddSeconds(-10);
        state.Receive(new AddressableStateValue
        {
            IsInitialized = true,
            UpdateDate = stamped,
            Value = Newtonsoft.Json.JsonConvert.SerializeObject(new Marker { Value = 2 })
        });

        state.Value.Value.Should().Be(2);
    }

    [Fact]
    public async Task SetValue_AfterDeploySwitch_WritesTheRowOfTheNewEpoch()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        var storage = Substitute.For<IStateStorage>();
        storage.Registry.Returns(TestStates.CreateRegistry());
        storage.Read<AddressableStateValue>(Arg.Any<StateIdentity>()).Returns(Task.FromResult(new AddressableStateValue()));

        var orleans = Substitute.For<IOrleans>();
        orleans.StateStorage.Returns(storage);
        orleans.Serializer.Returns(new JsonTestSerializer());

        var messaging = Substitute.For<IMessaging>();

        var state = new TestState(orleans, messaging);
        await state.OnDeployChanged(first, new Lifetime(TestLifetime));
        await state.OnDeployChanged(second, new Lifetime(TestLifetime));

        await state.SetValue(new Marker { Value = 3 });

        await storage.Received(1).Write(Arg.Is<StateWriteRequest>(request => IsOf(request, second)));
        await storage.DidNotReceive().Write(Arg.Is<StateWriteRequest>(request => IsOf(request, first)));
        state.Value.Value.Should().Be(3);
    }

    private static bool IsOf(StateWriteRequest request, Guid deployId)
    {
        return request.Records.Keys.Any(identity => identity.Key.Equals(deployId));
    }

    private class TestState(IOrleans orleans, IMessaging messaging)
        : DeploymentState<Marker>(orleans, messaging, NullLoggerFactory.Instance, new Marker())
    {
        public void Receive(AddressableStateValue state) => OnUpdate(state);
    }
}
