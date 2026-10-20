using Cluster.State;
using Common.Reactive;
using FluentAssertions;
using Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Tests.Fixtures;
using Xunit;

namespace Tests.Cluster;

[Collection(nameof(OrleansIntegrationCollection))]
public class ClusterFeaturesWriterTests(OrleansTestClusterFixture fixture)
    : IntegrationTestBase<OrleansTestClusterFixture>(fixture)
{
    [Fact]
    public async Task ConcurrentPatches_FromServicesWithStaleCopies_KeepEveryFlag()
    {
        var deployId = Guid.NewGuid();
        var lifetime = new Lifetime();

        // Two services of one deploy, each with its own local copy of the row.
        var coordinator = await Attach(deployId, lifetime);
        var gateway = await Attach(deployId, lifetime);

        // Both copies are stale for each other's change: a read-modify-write from the local copy
        // would write back the other flag with its old value.
        await Task.WhenAll(
            coordinator.SetSideEffectsEnabled(true),
            coordinator.SetMatchmakingEnabled(true),
            gateway.SetAcceptingConnections(true),
            gateway.SetSnapshotDiffGuardEnabled(true),
            gateway.SetCoordinatorUnhealthy(true));

        var reloaded = await Attach(deployId, lifetime);

        reloaded.Value.SideEffectsEnabled.Should().BeTrue();
        reloaded.Value.MatchmakingEnabled.Should().BeTrue();
        reloaded.Value.SnapshotDiffGuardEnabled.Should().BeTrue();
        reloaded.Value.AcceptingConnections.Should().BeTrue();
        reloaded.Value.CoordinatorUnhealthy.Should().BeTrue();
        reloaded.IsAcceptingConnections.Should().BeFalse();

        await WaitUntil(() => coordinator.Value.AcceptingConnections && coordinator.Value.CoordinatorUnhealthy);
        await WaitUntil(() => gateway.Value.SideEffectsEnabled);

        lifetime.Terminate();
    }

    [Fact]
    public async Task SequentialPatches_FromDifferentServices_AllApplied()
    {
        var deployId = Guid.NewGuid();
        var lifetime = new Lifetime();

        var first = await Attach(deployId, lifetime);
        var second = await Attach(deployId, lifetime);

        for (var i = 0; i < 10; i++)
        {
            var value = i % 2 == 0;
            await first.SetSideEffectsEnabled(value);
            await second.SetAcceptingConnections(value == false);
        }

        var reloaded = await Attach(deployId, lifetime);

        reloaded.Value.SideEffectsEnabled.Should().BeFalse();
        reloaded.Value.AcceptingConnections.Should().BeTrue();

        lifetime.Terminate();
    }

    [Fact]
    public async Task SetValue_IsRejected()
    {
        var features = await Attach(Guid.NewGuid(), new Lifetime());

        var act = () => features.SetValue(new ClusterFeaturesState { AcceptingConnections = true });

        await act.Should().ThrowAsync<NotSupportedException>();
    }

    private async Task<ClusterFeatures> Attach(Guid deployId, IReadOnlyLifetime lifetime)
    {
        var features = new ClusterFeatures(
            GetSiloService<IOrleans>(),
            GetSiloService<IMessaging>(),
            NullLoggerFactory.Instance);

        await features.OnDeployChanged(deployId, lifetime);
        return features;
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);

        while (condition() == false)
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException("Condition was not reached in 10 s");

            await Task.Delay(20, TestContext.Current.CancellationToken);
        }
    }
}
