using FluentAssertions;
using Framework.Tests.Fixtures;
using Game.ClientTelemetry;
using Internal;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shared;
using Xunit;

namespace Framework.Tests.ClientTelemetry;

// A close the client asked for keeps its status through a failed write: the retry of the timer is the same close, not a loss.
[Collection(nameof(ClientTelemetryCollection))]
public class ClientTelemetryCloseRetryTests : ClientTelemetryStorageTest
{
    public ClientTelemetryCloseRetryTests(ClientTelemetryClusterFixture cluster) : base(cluster)
    {
    }

    private readonly IClientTelemetrySwitch _switch = Substitute.For<IClientTelemetrySwitch>();

    private ClientTelemetryConnection Open()
    {
        _switch.IsEnabled.Returns(true);
        return new ClientTelemetryConnections(_switch, Cluster.Orleans, NullLogger<ClientTelemetryConnection>.Instance).Of(TestLifetime.Child());
    }

    [Fact]
    public async Task AFailedCloseAtTheSessionEnd_IsRepeatedByTheTimer_AsEnded()
    {
        var connection = Open();
        var id = (await connection.Start(new SharedClientTelemetry.SessionStart { Device = new SharedClientTelemetry.Device { Platform = "web" } })).SessionId;
        await connection.Accept(TelemetryWire.Batch(id, ["frame.dt"], [(1000, [10], false)]));

        Cluster.Storage.FailWrites(true);
        await connection.End(new SharedClientTelemetry.SessionEnd { SessionId = id });

        // Checked while the writes still fail: the timer of the grain ticks every 10 ms of real time and may store the retry
        // right after the writes come back.
        (await Session(id)).Status.Should().Be(TelemetrySessionStatus.Live, "nothing is stored yet");
        Cluster.Storage.FailWrites(false);

        // Past LostAfter too: the retry must not turn into the close of a silent client.
        Cluster.Clock.Advance(Cluster.Options.LostAfter + TimeSpan.FromSeconds(1));
        await TestWait.Until(async () => (await Reader.GetSession(id))?.Status != TelemetrySessionStatus.Live);

        var session = await Session(id);
        session.Status.Should().Be(TelemetrySessionStatus.Ended);
        session.Digest!.Summary.Samples.Should().Be(1);
    }
}
