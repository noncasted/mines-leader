using Cluster.Configs;
using FluentAssertions;
using Framework.Tests.Fixtures;
using Infrastructure;
using Infrastructure.State;
using Internal;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using NSubstitute;
using Xunit;

namespace Framework.Tests.State;

// AddressableState over a substituted storage and an in-memory channel: the load at start, the updates of the
// channel around it and the persisting write.
public class AddressableStateLoadTests
{
    public AddressableStateLoadTests()
    {
        _storage = Substitute.For<IStateStorage>();
        _storage.Registry.Returns(TestStates.CreateRegistry());

        var orleans = Substitute.For<IOrleans>();
        orleans.StateStorage.Returns(_storage);
        orleans.Serializer.Returns(new JsonTestSerializer());

        var messaging = Substitute.For<IMessaging>();
        messaging.RuntimeChannel.Returns(_channel);

        _state = new AddressableState<TransactionOptions>(
            new AddressableStateUtils(orleans, messaging, NullLoggerFactory.Instance));
    }

    private readonly IStateStorage _storage;
    private readonly InMemoryChannelClient _channel = new();
    private readonly AddressableState<TransactionOptions> _state;
    private readonly Lifetime _lifetime = new();

    [Fact]
    public async Task OnOrleansStarted_ReadFails_Throws()
    {
        _storage.Read<AddressableStateValue>(Arg.Any<StateIdentity>())
                .Returns(Task.FromException<AddressableStateValue>(new Exception("db down")));

        var start = () => _state.OnOrleansStarted(_lifetime);

        await start.Should().ThrowAsync<Exception>().WithMessage("db down");
    }

    [Fact]
    public async Task ConfigsInitializer_SetValueFails_Throws()
    {
        var storage = Substitute.For<IAddressableState<TransactionOptions>>();
        storage.IsInitialized.Returns(false);
        storage.SetValue(Arg.Any<TransactionOptions>()).Returns(Task.FromException(new Exception("db down")));

        var init = () => ConfigsInitializer.Init("config.missing", storage, NullLogger.Instance);

        await init.Should().ThrowAsync<Exception>().WithMessage("db down");
    }

    [Fact]
    public async Task OnOrleansStarted_UpdatePublishedDuringRead_NotLost()
    {
        var stored = Stored(new TransactionOptions { LockWaitSeconds = 1 }, DateTime.UtcNow);

        // The read takes its snapshot, then another host writes and publishes before this one listens.
        _storage.Read<AddressableStateValue>(Arg.Any<StateIdentity>()).Returns(_ => {
            _channel.Deliver(Stored(new TransactionOptions { LockWaitSeconds = 2 }, DateTime.UtcNow.AddSeconds(1)));
            return Task.FromResult(stored);
        });

        await _state.OnOrleansStarted(_lifetime);

        _state.Value.LockWaitSeconds.Should().Be(2);
    }

    [Fact]
    public async Task OnOrleansStarted_GapDetected_ReloadsFromStorage()
    {
        _storage.Read<AddressableStateValue>(Arg.Any<StateIdentity>())
                .Returns(Task.FromResult(Stored(new TransactionOptions { LockWaitSeconds = 1 }, DateTime.UtcNow)),
                    Task.FromResult(Stored(new TransactionOptions { LockWaitSeconds = 3 }, DateTime.UtcNow.AddSeconds(1))));

        await _state.OnOrleansStarted(_lifetime);
        _channel.ReportGap();

        await TestWait.Until(() => _state.Value.LockWaitSeconds == 3);
    }

    [Fact]
    public async Task SetValue_WriteFails_ValueUnchanged()
    {
        _storage.Read<AddressableStateValue>(Arg.Any<StateIdentity>())
                .Returns(Task.FromResult(Stored(new TransactionOptions { LockWaitSeconds = 1 }, DateTime.UtcNow)));
        _storage.Write(Arg.Any<StateWriteRequest>()).Returns(Task.FromException(new Exception("db down")));

        await _state.OnOrleansStarted(_lifetime);

        var set = () => _state.SetValue(new TransactionOptions { LockWaitSeconds = 9 });
        await set.Should().ThrowAsync<Exception>().WithMessage("db down");

        _state.Value.LockWaitSeconds.Should().Be(1, "the value that is not in the database must not be visible");
    }

    private static AddressableStateValue Stored(TransactionOptions value, DateTime updateDate)
    {
        return new AddressableStateValue
        {
            IsInitialized = true,
            UpdateDate = updateDate,
            Value = JsonConvert.SerializeObject(value)
        };
    }
}
