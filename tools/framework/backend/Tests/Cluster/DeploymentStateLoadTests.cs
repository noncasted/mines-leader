using Cluster.Deploy;
using FluentAssertions;
using Framework.Tests.Fixtures;
using Infrastructure;
using Infrastructure.State;
using Internal;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using NSubstitute;
using Xunit;

namespace Framework.Tests.Cluster;

// DeploymentState over a substituted storage and an in-memory channel: the load of an epoch, the updates of the
// channel around it, the order of the writes of hosts with different clocks and the persisting write.
public class DeploymentStateLoadTests
{
    public class Marker
    {
        public int Value { get; init; }
    }

    private readonly InMemoryChannelClient _channel = new();
    private readonly Lifetime _lifetime = new();

    [Fact]
    public async Task OnDeployChanged_UpdatePublishedDuringRead_NotLost()
    {
        var storage = CreateStorage();
        var stored = Stored(1, DateTime.UtcNow);

        // The read takes its snapshot, then another host writes and publishes before this one listens.
        storage.Read<AddressableStateValue>(Arg.Any<StateIdentity>()).Returns(_ => {
            _channel.Deliver(Stored(2, DateTime.UtcNow.AddSeconds(1)));
            return Task.FromResult(stored);
        });

        var state = CreateState(storage);
        await state.OnDeployChanged(Guid.NewGuid(), _lifetime);

        state.Value.Value.Should().Be(2);
    }

    [Fact]
    public async Task OnDeployChanged_GapDetected_ReloadsFromStorage()
    {
        var storage = CreateStorage();

        storage.Read<AddressableStateValue>(Arg.Any<StateIdentity>())
               .Returns(Task.FromResult(Stored(1, DateTime.UtcNow)),
                   Task.FromResult(Stored(3, DateTime.UtcNow.AddSeconds(1))));

        var state = CreateState(storage);
        await state.OnDeployChanged(Guid.NewGuid(), _lifetime);
        _channel.ReportGap();

        await TestWait.Until(() => state.Value.Value == 3);
    }

    [Fact]
    public async Task SetValue_AfterUpdateOfFasterClock_AppliedByOtherHosts()
    {
        var storage = CreateStorage();
        storage.Read<AddressableStateValue>(Arg.Any<StateIdentity>()).Returns(Task.FromResult(new AddressableStateValue()));

        var deployId = Guid.NewGuid();
        var writer = CreateState(storage);
        var reader = CreateState(storage, new InMemoryChannelClient());
        await writer.OnDeployChanged(deployId, _lifetime);
        await reader.OnDeployChanged(deployId, _lifetime);

        // A host whose clock runs a minute ahead wrote last; this host writes after it with its own clock.
        var ahead = Stored(1, DateTime.UtcNow.AddMinutes(1));
        writer.Receive(ahead);
        reader.Receive(ahead);

        await writer.SetValue(new Marker { Value = 2 });
        reader.Receive((AddressableStateValue)_channel.Published.Single());

        writer.Value.Value.Should().Be(2);
        reader.Value.Value.Should().Be(2, "a write made after seeing the previous value must replace it everywhere");
    }

    [Fact]
    public async Task SetValue_WriteFails_ValueUnchanged()
    {
        var storage = CreateStorage();
        storage.Read<AddressableStateValue>(Arg.Any<StateIdentity>()).Returns(Task.FromResult(Stored(1, DateTime.UtcNow)));
        storage.Write(Arg.Any<StateWriteRequest>()).Returns(Task.FromException(new Exception("db down")));

        var state = CreateState(storage);
        await state.OnDeployChanged(Guid.NewGuid(), _lifetime);

        var set = () => state.SetValue(new Marker { Value = 9 });
        await set.Should().ThrowAsync<Exception>().WithMessage("db down");

        state.Value.Value.Should().Be(1, "the value that is not in the database must not be visible");
    }

    private static IStateStorage CreateStorage()
    {
        var storage = Substitute.For<IStateStorage>();
        storage.Registry.Returns(TestStates.CreateRegistry());
        return storage;
    }

    private TestState CreateState(IStateStorage storage, InMemoryChannelClient? channel = null)
    {
        var orleans = Substitute.For<IOrleans>();
        orleans.StateStorage.Returns(storage);
        orleans.Serializer.Returns(new JsonTestSerializer());

        var messaging = Substitute.For<IMessaging>();
        messaging.RuntimeChannel.Returns(channel ?? _channel);

        return new TestState(orleans, messaging);
    }

    private static AddressableStateValue Stored(int value, DateTime updateDate)
    {
        return new AddressableStateValue
        {
            IsInitialized = true,
            UpdateDate = updateDate,
            Value = JsonConvert.SerializeObject(new Marker { Value = value })
        };
    }

    private class TestState(IOrleans orleans, IMessaging messaging)
        : DeploymentState<Marker>(orleans, messaging, NullLoggerFactory.Instance, new Marker())
    {
        public void Receive(AddressableStateValue state) => OnUpdate(state);
    }
}
