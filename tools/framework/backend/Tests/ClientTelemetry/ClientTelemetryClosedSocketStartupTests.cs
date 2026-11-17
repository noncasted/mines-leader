using FluentAssertions;
using Game.ClientTelemetry;
using Internal;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shared;
using Xunit;

namespace Framework.Tests.ClientTelemetry;

// The contract: a closed socket changes nothing, the startup traces included.
[Collection(nameof(ClientTelemetryCollection))]
public class ClientTelemetryClosedSocketStartupTests : ClientTelemetryStorageTest
{
    public ClientTelemetryClosedSocketStartupTests(ClientTelemetryClusterFixture cluster) : base(cluster)
    {
    }

    [Fact]
    public async Task AStartupTrace_AfterTheEndOfTheSocket_CreatesNoRecord()
    {
        var telemetrySwitch = Substitute.For<IClientTelemetrySwitch>();
        telemetrySwitch.IsEnabled.Returns(true);

        var lifetime = new Lifetime();
        var connection = new ClientTelemetryConnections(telemetrySwitch, Cluster.Orleans, NullLogger<ClientTelemetryConnection>.Instance).Of(lifetime);
        lifetime.Terminate();

        var answer = await connection.AcceptStartup(new SharedClientTelemetry.StartupTrace
        {
            Device = new SharedClientTelemetry.Device { Platform = "web" },
            Name = "Boot",
            Spans = [new SharedClientTelemetry.StartupSpan { ParentIndex = -1, Name = "Stage", DurationMs = 10 }]
        }, default);

        answer.Enabled.Should().BeFalse();
        answer.StartupId.Should().Be(Guid.Empty);
    }
}
