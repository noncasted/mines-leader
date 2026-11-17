using Common.Network;
using DeploySetup;
using FluentAssertions;
using Game.ClientTelemetry;
using Internal;
using MemoryPack;
using MetaGateway.UserFlow.Commands;
using MetaGateway.UserFlow.Connection;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shared;
using Xunit;

namespace Tests.Meta.Telemetry;

// The wiring of the client telemetry in this game: the logic of the telemetry is tested in the framework.
public class ClientTelemetryCommandsTests
{
    private readonly IClientTelemetryConnections _connections = new OneConnection();
    private readonly IUserSession _session = new LifetimeSession();

    [Fact]
    public async Task Start_GoesToTheConnectionOfTheSocket()
    {
        IUserCommand command = new ClientTelemetryCommands.Start(_connections);

        var response = await command.Execute(_session, new SharedClientTelemetry.SessionStart());

        response.Should().BeOfType<SharedClientTelemetry.SessionStarted>().Which.Enabled.Should().BeFalse();
    }

    [Fact]
    public async Task Batch_GoesToTheConnectionOfTheSocket()
    {
        IUserCommand command = new ClientTelemetryCommands.Batch(_connections);

        var response = await command.Execute(_session, new SharedClientTelemetry.Batch { SessionId = Guid.NewGuid() });

        response.Should().BeOfType<SharedClientTelemetry.BatchResult>().Which.Enabled.Should().BeFalse();
    }

    [Fact]
    public async Task End_GoesToTheConnectionOfTheSocket()
    {
        IUserCommand command = new ClientTelemetryCommands.End(_connections);

        var response = await command.Execute(_session, new SharedClientTelemetry.SessionEnd { SessionId = Guid.NewGuid() });

        response.Should().BeOfType<EmptyResponse>();
    }

    [Fact]
    public async Task StartupTrace_GoesToTheConnectionOfTheSocket()
    {
        IUserCommand command = new ClientTelemetryCommands.StartupTrace(_connections);

        var response = await command.Execute(_session, new SharedClientTelemetry.StartupTrace());

        response.Should().BeOfType<SharedClientTelemetry.StartupTraceResult>().Which.Enabled.Should().BeFalse();
    }

    [Fact]
    public void Register_EveryRequestOfTheContractHasItsCommand()
    {
        var builder = WebApplication.CreateBuilder();

        builder.AddClientTelemetryCommands();

        builder.Services.AddSingleton(_connections);

        var commands = builder.Services.BuildServiceProvider()
                              .GetServices<IUserCommand>()
                              .Select(command => command.RequestType);

        commands.Should().BeEquivalentTo([
            typeof(SharedClientTelemetry.SessionStart),
            typeof(SharedClientTelemetry.Batch),
            typeof(SharedClientTelemetry.SessionEnd),
            typeof(SharedClientTelemetry.StartupTrace)
        ]);
    }

    [Theory]
    [MemberData(nameof(Messages))]
    public void Union_KeepsTheTypeOfEveryMessage(INetworkContext message)
    {
        var bytes = MemoryPackSerializer.Serialize(message);

        MemoryPackSerializer.Deserialize<INetworkContext>(bytes).Should().BeOfType(message.GetType());
    }

    public static TheoryData<INetworkContext> Messages()
    {
        return new TheoryData<INetworkContext>
        {
            new SharedClientTelemetry.SessionStart(),
            new SharedClientTelemetry.SessionStarted(),
            new SharedClientTelemetry.Batch(),
            new SharedClientTelemetry.BatchResult(),
            new SharedClientTelemetry.SessionEnd(),
            new SharedClientTelemetry.StartupTrace(),
            new SharedClientTelemetry.StartupTraceResult()
        };
    }

    // The states of the module are in the registry of the composition: the migrator creates their tables.
    [Fact]
    public void States_AreInTheRegistryOfTheMigrator()
    {
        var states = DeployStates.All
                                 .Where(info => info.Type.Namespace == "Game.ClientTelemetry")
                                 .Select(info => (info.Type.Name, info.TableName, info.StateName, info.KeyType));

        states.Should().BeEquivalentTo([
            ("ClientTelemetrySettingsState", "configs", "client_telemetry_settings", GrainKeyType.String),
            ("TelemetrySessionLogState", "state_client_telemetry_logs", "client_telemetry_log", GrainKeyType.Guid),
            ("TelemetrySessionState", "state_client_telemetry_sessions", "client_telemetry_session", GrainKeyType.Guid),
            ("TelemetryStartupState", "state_client_telemetry_startups", "client_telemetry_startup", GrainKeyType.Guid),
            ("TelemetryStartupTraceState", "state_client_telemetry_startup_traces", "client_telemetry_startup_trace", GrainKeyType.Guid)
        ]);
    }

    private class OneConnection : IClientTelemetryConnections
    {
        private readonly ClientTelemetryConnection _connection = new(new OffSwitch(), null!, NullLogger<ClientTelemetryConnection>.Instance);

        public ClientTelemetryConnection Of(IReadOnlyLifetime connection)
        {
            return _connection;
        }
    }

    private class OffSwitch : IClientTelemetrySwitch
    {
        public bool IsLoaded => true;
        public bool IsEnabled => false;

        public void View(IReadOnlyLifetime lifetime, Action listener)
        {
            listener();
        }

        public Task Set(bool enabled)
        {
            throw new InvalidOperationException("The test does not change the flag");
        }
    }

    private class LifetimeSession : IUserSession
    {
        public IConnection Connection => throw new InvalidOperationException("The commands of the telemetry do not use the connection");
        public Guid UserId => throw new InvalidOperationException("The telemetry knows no user");
        public IReadOnlyLifetime Lifetime { get; } = new Lifetime();
    }
}
