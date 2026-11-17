using Cluster.Deploy;
using Cluster.Discovery;
using Cluster.Monitoring;
using Internal;
using FluentAssertions;
using Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Framework.Tests.Fixtures;
using Xunit;

namespace Framework.Tests.Cluster;

// live monitoring data written by several gateways of one deploy. Every gateway owns its slice,
// so two gateways no longer overwrite each other's users / sessions.
[Collection(nameof(OrleansIntegrationCollection))]
public class ServiceSlicedStateTests(OrleansTestClusterFixture fixture)
    : IntegrationTestBase<OrleansTestClusterFixture>(fixture)
{
    [Fact]
    public async Task TwoGateways_ConcurrentSlices_BothKept()
    {
        var deployId = Guid.NewGuid();
        var lifetime = TestLifetime.Child();

        var first = await Attach(deployId, lifetime);
        var second = await Attach(deployId, lifetime);
        await Register(deployId, first.Environment, second.Environment);

        var firstUser = new ConnectedUserEntry { UserId = Guid.NewGuid() };
        var secondUser = new ConnectedUserEntry { UserId = Guid.NewGuid() };

        await Task.WhenAll(
            first.State.SetSlice([firstUser]),
            second.State.SetSlice([secondUser]));

        var reloaded = await Attach(deployId, lifetime);

        reloaded.State.Value.AllUsers().Select(u => u.UserId).Should()
                .BeEquivalentTo([firstUser.UserId, secondUser.UserId]);

        await TestWait.Until(() => first.State.Value.AllUsers().Count == 2);
        await TestWait.Until(() => second.State.Value.AllUsers().Count == 2);

        lifetime.Terminate();
    }

    [Fact]
    public async Task SliceUpdate_ReplacesOnlyOwnSlice()
    {
        var deployId = Guid.NewGuid();
        var lifetime = TestLifetime.Child();

        var first = await Attach(deployId, lifetime);
        var second = await Attach(deployId, lifetime);
        await Register(deployId, first.Environment, second.Environment);

        var secondUser = new ConnectedUserEntry { UserId = Guid.NewGuid() };

        await first.State.SetSlice([new ConnectedUserEntry { UserId = Guid.NewGuid() }]);
        await second.State.SetSlice([secondUser]);
        await first.State.SetSlice([]);

        var reloaded = await Attach(deployId, lifetime);

        reloaded.State.Value.AllUsers().Select(u => u.UserId).Should().Equal(secondUser.UserId);

        lifetime.Terminate();
    }

    [Fact]
    public async Task ServiceGoneFromDiscovery_SliceDropped()
    {
        var deployId = Guid.NewGuid();
        var lifetime = TestLifetime.Child();

        var alive = await Attach(deployId, lifetime);
        var crashed = await Attach(deployId, lifetime);

        // The crashed gateway wrote its slice and never cleared it; it is not in discovery any more.
        await Register(deployId, alive.Environment);
        await crashed.State.SetSlice([new ConnectedUserEntry { UserId = Guid.NewGuid() }]);

        var aliveUser = new ConnectedUserEntry { UserId = Guid.NewGuid() };
        await alive.State.SetSlice([aliveUser]);

        var reloaded = await Attach(deployId, lifetime);

        reloaded.State.Value.Slices.Keys.Should().Equal(alive.Environment.ServiceId);
        reloaded.State.Value.AllUsers().Select(u => u.UserId).Should().Equal(aliveUser.UserId);

        lifetime.Terminate();
    }

    [Fact]
    public async Task SetValue_IsRejected()
    {
        var attached = await Attach(Guid.NewGuid(), TestLifetime);

        var act = () => attached.State.SetValue(new ConnectedUsersLiveData());

        await act.Should().ThrowAsync<NotSupportedException>();
    }

    private async Task<Attached> Attach(Guid deployId, IReadOnlyLifetime lifetime)
    {
        var environment = new ServiceEnvironment { Tag = TestRoles.Meta };

        var state = new ServiceSlicedState<ConnectedUsersLiveData, List<ConnectedUserEntry>>(
            GetSiloService<IOrleans>(),
            GetSiloService<IMessaging>(),
            NullLoggerFactory.Instance,
            environment,
            new ConnectedUsersLiveData());

        await state.OnDeployChanged(deployId, lifetime);
        return new Attached(state, environment);
    }

    private async Task Register(Guid deployId, params IServiceEnvironment[] services)
    {
        var storage = GetGrain<IServiceDiscoveryStorage>(deployId);

        foreach (var service in services)
        {
            await storage.Update(new ServiceOverview
            {
                Id = service.ServiceId,
                Tag = service.Tag,
                UpdateTime = DateTime.UtcNow
            });
        }
    }

    private record Attached(
        ServiceSlicedState<ConnectedUsersLiveData, List<ConnectedUserEntry>> State,
        IServiceEnvironment Environment);
}
