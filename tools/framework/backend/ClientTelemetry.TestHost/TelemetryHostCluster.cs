using Framework.Tests.ClientTelemetry;
using Game.ClientTelemetry;

namespace Framework.ClientTelemetry.TestHost;

// The silo of the tests on the real clock and the default limits, but the timer of a lost session: a look at the host does not wait
// for the one of a deploy.
public sealed class TelemetryHostCluster : ClientTelemetryClusterFixture
{
    public TelemetryHostCluster(TimeSpan lostAfter)
    {
        Options = new ClientTelemetryStorageOptions { LostAfter = lostAfter };
    }

    public override ClientTelemetryStorageOptions Options { get; }

    protected override TimeProvider SessionTime => TimeProvider.System;
}
