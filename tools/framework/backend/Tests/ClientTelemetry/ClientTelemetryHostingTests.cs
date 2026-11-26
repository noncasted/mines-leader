using FluentAssertions;
using Game.ClientTelemetry;
using Infrastructure;
using Infrastructure.State;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Shared;
using Xunit;

namespace Framework.Tests.ClientTelemetry;

public class ClientTelemetryHostingTests
{
    [Fact]
    public void AGateway_GetsTheConnectionsAndTheReader_AndAConsoleOnlyTheReader_AndASiloTheSweep()
    {
        var gateway = Provider(builder => builder.AddClientTelemetry());
        gateway.GetRequiredService<IClientTelemetryConnections>().Should().NotBeNull();
        gateway.GetRequiredService<IClientTelemetryReader>().Should().NotBeNull();
        gateway.GetServices<ILocalSetupCompleted>().Should().ContainSingle(service => service is TelemetrySessionCollection);
        TheSwitchStartsWithTheHost(gateway);

        var console = Provider(builder => builder.AddClientTelemetryReader());
        console.GetRequiredService<IClientTelemetryReader>().Should().NotBeNull();
        console.GetService<IClientTelemetryConnections>().Should().BeNull();
        TheSwitchStartsWithTheHost(console);

        var silo = Provider(builder => builder.AddClientTelemetrySessions());
        silo.GetServices<ILocalSetupCompleted>().Select(service => service.GetType())
            .Should().BeEquivalentTo([typeof(TelemetrySessionCollection), typeof(TelemetryStartupCollection), typeof(ClientTelemetrySweep)]);
        silo.GetRequiredService<ITelemetrySessionCollection>().Should().BeSameAs(silo.GetRequiredService<TelemetrySessionCollection>());
        silo.GetService<IClientTelemetrySwitch>().Should().BeNull();
    }

    private static void TheSwitchStartsWithTheHost(IServiceProvider host)
    {
        host.GetServices<IOrleansStarted>().Should().ContainSingle()
            .Which.Should().BeSameAs(host.GetRequiredService<IClientTelemetrySwitch>());
    }

    private static ServiceProvider Provider(Action<IHostApplicationBuilder> add)
    {
        var builder = Host.CreateApplicationBuilder();

        var orleans = Substitute.For<IOrleans>();
        orleans.StateStorage.Registry.Get<ClientTelemetrySettingsState>().Returns(new GrainStateInfo
        {
            Name = ClientTelemetrySettingsState.StateName,
            TableName = "configs",
            KeyType = GrainKeyType.String,
            Type = typeof(ClientTelemetrySettingsState)
        });

        builder.Services.AddSingleton(orleans);
        builder.Services.AddSingleton(Substitute.For<IStateStorage>());
        builder.Services.AddSingleton(Substitute.For<IMessaging>());
        builder.Services.AddSingleton(Substitute.For<IGrainStatesRegistry>());
        add(builder);

        return builder.Services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });
    }
}
