using Cluster.State;
using Internal;
using FluentAssertions;
using Infrastructure;
using Orleans.Serialization;
using Framework.Tests.Fixtures;
using Xunit;

namespace Framework.Tests.Messaging;

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

        await messaging.ListenChannel<ClusterFeaturesState>(TestLifetime, channelId, msg =>
        {
            received.TrySetResult(msg);
        });

        var state = new ClusterFeaturesState();
        state.SetAcceptingConnections(true);
        state.SetSideEffectsEnabled(true);

        await messaging.PublishChannel(channelId, state);

        var result = await received.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        result.AcceptingConnections.Should().BeTrue();
        result.SideEffectsEnabled.Should().BeTrue();
    }
}
