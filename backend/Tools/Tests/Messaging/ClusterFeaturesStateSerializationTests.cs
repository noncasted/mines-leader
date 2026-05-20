using Cluster.State;
using Common.Reactive;
using FluentAssertions;
using Infrastructure;
using Orleans.Serialization;
using Tests.Fixtures;
using Xunit;

namespace Tests.Messaging;

[Collection(nameof(OrleansIntegrationCollection))]
public class ClusterFeaturesStateSerializationTests(OrleansTestClusterFixture fixture)
    : IntegrationTestBase<OrleansTestClusterFixture>(fixture)
{
    [Fact]
    public async Task Publish_ClusterFeaturesState_Received()
    {
        var channelId = new TestChannelId($"test-features-{Guid.NewGuid():N}");
        var received = new TaskCompletionSource<ClusterFeaturesState>();
        var messaging = GetSiloService<IMessaging>();

        await messaging.ListenChannel<ClusterFeaturesState>(new Lifetime(), channelId, msg =>
        {
            received.TrySetResult(msg);
        });

        var state = new ClusterFeaturesState
        {
            AcceptingConnections = true,
            MatchmakingEnabled = true,
            SideEffectsEnabled = true,
            SnapshotDiffGuardEnabled = true
        };

        await messaging.PublishChannel(channelId, state);

        var result = await received.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        result.AcceptingConnections.Should().BeTrue();
        result.MatchmakingEnabled.Should().BeTrue();
        result.SideEffectsEnabled.Should().BeTrue();
        result.SnapshotDiffGuardEnabled.Should().BeTrue();
    }
}
