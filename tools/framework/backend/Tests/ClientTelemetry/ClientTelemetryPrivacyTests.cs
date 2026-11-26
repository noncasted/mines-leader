using FluentAssertions;
using Framework.Tests.Fixtures;
using Game.ClientTelemetry;
using Infrastructure;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shared;
using Xunit;

namespace Framework.Tests.ClientTelemetry;

// What a client sends stays out of the logs, what is not on the closed lists stays out of the states.
[Collection(nameof(ClientTelemetryCollection))]
public class ClientTelemetryPrivacyTests : ClientTelemetryStorageTest
{
    public ClientTelemetryPrivacyTests(ClientTelemetryClusterFixture cluster) : base(cluster)
    {
    }

    private const string _marker = "PrivacyMarker";

    [Fact]
    public async Task States_HoldNothingOutOfTheLists()
    {
        var telemetrySwitch = Substitute.For<IClientTelemetrySwitch>();
        telemetrySwitch.IsEnabled.Returns(true);

        var connection = new ClientTelemetryConnection(telemetrySwitch, Cluster.Orleans, Substitute.For<ILogger<ClientTelemetryConnection>>());
        var id = (await connection.Start(new SharedClientTelemetry.SessionStart
        {
            Device = new SharedClientTelemetry.Device { Platform = $"{_marker} Ivan", Gpu = $"{_marker}@mail.ru" },
            Tags = [$"{_marker} tag"]
        })).SessionId;

        await connection.Accept(TelemetryWire.Batch(id, [$"{_marker} name"], [(1, [1], false)], marks: [(1, $"{_marker} mark")]));
        await connection.End(new SharedClientTelemetry.SessionEnd { SessionId = id });

        var states = new[] { await RawState("state_client_telemetry_sessions", id), await RawState("state_client_telemetry_logs", id) };

        states.Should().AllSatisfy(state => {
            state.Should().NotBeNull();
            state.Should().NotContain(_marker);
        });
    }

    [Fact]
    public async Task AFailedBatch_LogsNothingOfTheClient()
    {
        var logs = new CapturingLogs();
        using var factory = LoggerFactory.Create(builder => builder.AddProvider(logs));

        var telemetrySwitch = Substitute.For<IClientTelemetrySwitch>();
        telemetrySwitch.IsEnabled.Returns(true);

        var orleans = Substitute.For<IOrleans>();
        var grain = Substitute.For<IClientTelemetrySession>();
        grain.Accept(Arg.Any<TelemetryBatch>()).Returns<Task<bool>>(_ => throw new InvalidOperationException("the grain is gone"));
        orleans.Grains.GetGrain<IClientTelemetrySession>(Arg.Any<Guid>(), Arg.Any<string?>()).Returns(grain);

        var connection = new ClientTelemetryConnection(telemetrySwitch, orleans, factory.CreateLogger<ClientTelemetryConnection>());
        var id = (await connection.Start(new SharedClientTelemetry.SessionStart())).SessionId;

        var result = await connection.Accept(TelemetryWire.Batch(id, errors: [TelemetryWire.Error(1, SharedClientTelemetry.ErrorLevel.Error, _marker)]));

        result.Enabled.Should().BeFalse();
        logs.Lines.Should().Contain(line => line.Contains("A batch is not taken"));
        logs.Lines.Should().NotContain(line => line.Contains(_marker));
    }
}
