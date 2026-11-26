using FluentAssertions;
using Game.ClientTelemetry;
using Infrastructure;
using Xunit;

namespace Framework.Tests.ClientTelemetry;

// An activation without memory that finds its row open: what is left is closed as lost, never deleted as expired.
[Collection(nameof(ClientTelemetryCollection))]
public class ClientTelemetrySweepOpenRowTests : ClientTelemetryStorageTest
{
    public ClientTelemetrySweepOpenRowTests(ClientTelemetryClusterFixture cluster) : base(cluster)
    {
    }

    [Fact]
    public async Task TheSweep_OfARowAStartStoredButFailed_ClosesItAsLost()
    {
        var id = Guid.NewGuid();
        var grain = Cluster.Orleans.GetGrain<IClientTelemetrySession>(id);
        var step = Request(id, 1, ["a"], [(1000, [1])]);

        Cluster.Storage.FailAfterStoring();
        var start = () => grain.Start(step.Device, step.Tags, null, null);
        await start.Should().ThrowAsync<Exception>();
        Cluster.Storage.FailWrites(false);

        await grain.Sweep();

        (await RawState("state_client_telemetry_sessions", id)).Should().NotBeNull("an open row is not expired");
        (await Session(id)).Status.Should().Be(TelemetrySessionStatus.Lost);
    }
}
