using Framework.Tests.Fixtures;
using Game.ClientTelemetry;
using Infrastructure;
using Infrastructure.State;
using Internal;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shared;
using Xunit;

namespace Framework.Tests.ClientTelemetry;

// The switch without a database: the row comes from a captured channel listener, on the threads of the test.
public class ClientTelemetrySwitchThreadTests
{
    // The toggles of the console view the switch from their circuits while the channel applies the row.
    [Fact]
    public async Task ConcurrentViews_WhileTheRowIsApplied_LoseNothing()
    {
        Func<AddressableStateValue, Task>? deliver = null;

        var orleans = Substitute.For<IOrleans>();
        orleans.StateStorage.Registry.Get<ClientTelemetrySettingsState>().Returns(new GrainStateInfo
        {
            TableName = "telemetry", KeyType = GrainKeyType.String, Type = typeof(ClientTelemetrySettingsState), Name = ClientTelemetrySettingsState.StateName
        });
        orleans.StateStorage.Read<AddressableStateValue>(Arg.Any<StateIdentity>()).Returns(new AddressableStateValue());
        orleans.Serializer.Deserialize<ClientTelemetrySettingsState>(Arg.Any<string>())
               .Returns(call => new ClientTelemetrySettingsState { IsEnabled = int.Parse(call.Arg<string>()) % 2 == 1 });

        var messaging = Substitute.For<IMessaging>();
        messaging.RuntimeChannel
                 .Listen(Arg.Any<IReadOnlyLifetime>(), Arg.Any<IRuntimeChannelId>(), Arg.Do<Func<AddressableStateValue, Task>>(listener => deliver = listener), Arg.Any<Action?>())
                 .Returns(Task.CompletedTask);

        var telemetrySwitch = new ClientTelemetrySwitch(new AddressableStateUtils(orleans, messaging, NullLoggerFactory.Instance));
        var lifetime = new Lifetime();
        await telemetrySwitch.OnOrleansStarted(lifetime);
        var start = DateTime.UtcNow;

        await SubscriptionStress.Run<bool>(
            (subscriber, listener) => telemetrySwitch.View(subscriber, () => listener(telemetrySwitch.IsEnabled)),
            index => deliver!(new AddressableStateValue { IsInitialized = true, UpdateDate = start.AddTicks(index), Value = index.ToString() }),
            enabled => enabled ? 1 : 0,
            index => index % 2);

        lifetime.Terminate();
    }
}
