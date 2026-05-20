using Cluster.Deploy;
using Cluster.Discovery;
using FluentAssertions;
using Infrastructure;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Orchestration;
using Tests.Fixtures;
using Xunit;

namespace Tests.Cluster;

[CollectionDefinition("ClusterFailover")]
public class ClusterFailoverCollection : ICollectionFixture<OrleansTestClusterFixture> { }

[Collection("ClusterFailover")]
public class CoordinatorFailoverTests : IntegrationTestBase<OrleansTestClusterFixture>
{
    public CoordinatorFailoverTests(OrleansTestClusterFixture fixture) : base(fixture) { }

    [Fact]
    public async Task HealthCheck_StaleHeartbeat_ReturnsUnhealthy()
    {
        var deployId = Guid.NewGuid();

        // Arrange: initialize grain and mark ready
        var grain = GetGrain<IDeployManagement>(deployId);
        await grain.Initialize();
        await grain.MarkCoordinatorReady();

        // Wait for heartbeat to go stale
        await Task.Delay(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        var deployContext = Substitute.For<IDeployContext>();
        deployContext.DeployId.Returns(deployId);

        var orleans = GetSiloService<IOrleans>();
        var options = Options.Create(new CoordinatorHealthOptions { StaleThreshold = TimeSpan.FromMilliseconds(500) });

        var healthCheck = new CoordinatorReadyHealthCheck(deployContext, orleans, options);

        // Act
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("stale");
    }

    [Fact]
    public async Task HealthCheck_FreshHeartbeat_ReturnsHealthy()
    {
        var deployId = Guid.NewGuid();

        // Arrange
        var grain = GetGrain<IDeployManagement>(deployId);
        await grain.Initialize();
        await grain.MarkCoordinatorReady();

        var deployContext = Substitute.For<IDeployContext>();
        deployContext.DeployId.Returns(deployId);

        var orleans = GetSiloService<IOrleans>();
        var options = Options.Create(new CoordinatorHealthOptions { StaleThreshold = TimeSpan.FromSeconds(15) });

        var healthCheck = new CoordinatorReadyHealthCheck(deployContext, orleans, options);

        // Act
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task HealthCheck_EmptyDeployId_ReturnsUnhealthy()
    {
        var deployContext = Substitute.For<IDeployContext>();
        deployContext.DeployId.Returns(Guid.Empty);

        var orleans = GetSiloService<IOrleans>();
        var options = Options.Create(new CoordinatorHealthOptions());

        var healthCheck = new CoordinatorReadyHealthCheck(deployContext, orleans, options);

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("not assigned");
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
        environment.Tag.Returns(ServiceTag.Meta);

        var orleans = GetSiloService<IOrleans>();
        var logger = Substitute.For<ILogger<ServiceDiscovery>>();

        var discovery = new ServiceDiscovery(orleans, deployContext, environment, logger);
        await discovery.Push();

        // Verify registered
        var storageGrain = orleans.GetGrain<IServiceDiscoveryStorage>(deployId);
        var before = await storageGrain.Update(new ServiceOverview
        {
            Id = serviceId,
            Tag = ServiceTag.Meta,
            UpdateTime = DateTime.UtcNow
        });
        before.Should().ContainKey(serviceId);

        // Act
        await discovery.Unregister();

        // Assert
        var after = await storageGrain.Update(new ServiceOverview
        {
            Id = Guid.NewGuid(),
            Tag = ServiceTag.Game,
            UpdateTime = DateTime.UtcNow
        });
        after.Should().NotContainKey(serviceId);
    }
}
