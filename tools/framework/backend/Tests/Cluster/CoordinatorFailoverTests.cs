using Cluster.Deploy;
using Cluster.Discovery;
using FluentAssertions;
using Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Framework.Tests.Fixtures;
using Xunit;

namespace Framework.Tests.Cluster;

// The deploy management and the discovery under a coordinator failover: grains of fresh deploy ids, nothing that needs a
// cluster of its own.
[Collection(nameof(OrleansIntegrationCollection))]
public class CoordinatorFailoverTests : IntegrationTestBase<OrleansTestClusterFixture>
{
    public CoordinatorFailoverTests(OrleansTestClusterFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task DeployManagement_Initialize_SetsReadyFalse()
    {
        var deployId = Guid.NewGuid();
        var grain = GetGrain<IDeployManagement>(deployId);

        await grain.Initialize();
        var state = await grain.GetState();

        state.CoordinatorReady.Should().BeFalse();
        state.DeployId.Should().Be(deployId);
        state.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task DeployManagement_MarkReady_SetsReadyTrue()
    {
        var deployId = Guid.NewGuid();
        var grain = GetGrain<IDeployManagement>(deployId);

        await grain.Initialize();
        await grain.MarkCoordinatorReady();
        var state = await grain.GetState();

        state.CoordinatorReady.Should().BeTrue();
    }

    [Fact]
    public async Task DeployManagement_Heartbeat_UpdatesLastHeartbeat()
    {
        var deployId = Guid.NewGuid();
        var grain = GetGrain<IDeployManagement>(deployId);

        await grain.Initialize();
        var before = (await grain.GetState()).LastHeartbeat;

        await Task.Delay(50, TestContext.Current.CancellationToken);
        await grain.Heartbeat();
        var after = (await grain.GetState()).LastHeartbeat;

        after.Should().BeAfter(before);
    }

    [Fact]
    public async Task ServiceDiscovery_Unregister_RemovesEntry()
    {
        var deployId = Guid.NewGuid();
        var serviceId = Guid.NewGuid();

        var deployContext = Substitute.For<IDeployContext>();
        deployContext.DeployId.Returns(deployId);

        var environment = Substitute.For<IServiceEnvironment>();
        environment.ServiceId.Returns(serviceId);
        environment.Tag.Returns(TestRoles.Meta);

        var orleans = GetSiloService<IOrleans>();
        var logger = Substitute.For<ILogger<ServiceDiscovery>>();

        var discovery = new ServiceDiscovery(orleans, deployContext, environment,
            Options.Create(new ServiceDiscoveryOptions()), logger);
        await discovery.Push();

        // Verify registered
        var storageGrain = orleans.GetGrain<IServiceDiscoveryStorage>(deployId);
        var before = await storageGrain.Update(new ServiceOverview
        {
            Id = serviceId,
            Tag = TestRoles.Meta,
            UpdateTime = DateTime.UtcNow
        });
        before.Should().ContainKey(serviceId);

        // Act
        await discovery.Unregister();

        // Assert
        var after = await storageGrain.Update(new ServiceOverview
        {
            Id = Guid.NewGuid(),
            Tag = TestRoles.Game,
            UpdateTime = DateTime.UtcNow
        });
        after.Should().NotContainKey(serviceId);
    }
}
