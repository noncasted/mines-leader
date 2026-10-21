using Cluster.Deploy;
using Cluster.Discovery;
using Cluster.State;
using Common.Reactive;
using FluentAssertions;
using Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Tests.Cluster;

public class DeployHealthCheckerTests
{
    private readonly Guid _deployId = Guid.NewGuid();
    private readonly ClusterFeaturesState _features = new() { AcceptingConnections = true };
    private readonly IClusterFeatures _clusterFeatures = Substitute.For<IClusterFeatures>();
    private readonly IDeployManagement _deployGrain = Substitute.For<IDeployManagement>();
    private readonly IDeployContext _deployContext = Substitute.For<IDeployContext>();
    private readonly IMessaging _messaging = Substitute.For<IMessaging>();
    private DateTime _lastHeartbeat;

    public DeployHealthCheckerTests()
    {
        _clusterFeatures.Value.Returns(_ => _features);
        _clusterFeatures.SetCoordinatorUnhealthy(Arg.Any<bool>()).Returns(call => {
            _features.CoordinatorUnhealthy = call.Arg<bool>();
            return Task.CompletedTask;
        });

        _deployGrain.GetState().Returns(_ => new DeployState
        {
            DeployId = _deployId,
            LastHeartbeat = _lastHeartbeat,
            CoordinatorReady = true
        });

        _deployContext.DeployId.Returns(_ => _deployId);

        _messaging.RuntimePipe.Send<DeployIdResponse>(Arg.Any<IRuntimePipeId>(), Arg.Any<object>())
                  .Returns(_ => new DeployIdResponse { DeployId = _deployId });
    }

    [Fact]
    public async Task StaleHeartbeat_ClosesDeploy_FreshHeartbeat_ReopensIt()
    {
        var checker = CreateChecker(GameRoles.Meta);

        _lastHeartbeat = DateTime.UtcNow - TimeSpan.FromMinutes(1);
        await checker.Poll(new Lifetime());

        _features.CoordinatorUnhealthy.Should().BeTrue();

        _lastHeartbeat = DateTime.UtcNow;
        await checker.Poll(new Lifetime());

        _features.CoordinatorUnhealthy.Should().BeFalse();
        _features.AcceptingConnections.Should().BeTrue("recovery must not touch the operator switch");
        await _clusterFeatures.DidNotReceive().SetAcceptingConnections(Arg.Any<bool>());
    }

    [Fact]
    public async Task FreshHeartbeat_ReopensDeploy_ClosedByAnotherService()
    {
        _features.CoordinatorUnhealthy = true;
        _lastHeartbeat = DateTime.UtcNow;

        // The coordinator never closes the deploy itself, but reopens it like everyone else.
        await CreateChecker(ServiceTag.Coordinator).Poll(new Lifetime());

        _features.CoordinatorUnhealthy.Should().BeFalse();
    }

    [Fact]
    public async Task FreshHeartbeat_HealthyDeploy_WritesNothing()
    {
        _lastHeartbeat = DateTime.UtcNow;

        await CreateChecker(GameRoles.Game).Poll(new Lifetime());

        await _clusterFeatures.DidNotReceive().SetCoordinatorUnhealthy(Arg.Any<bool>());
    }

    [Fact]
    public async Task StaleHeartbeat_SeenByCoordinator_DoesNotCloseDeploy()
    {
        _lastHeartbeat = DateTime.UtcNow - TimeSpan.FromMinutes(1);

        await CreateChecker(ServiceTag.Coordinator).Poll(new Lifetime());

        await _clusterFeatures.DidNotReceive().SetCoordinatorUnhealthy(Arg.Any<bool>());
    }

    [Fact]
    public async Task EpochSwitch_Fails_Propagates()
    {
        _lastHeartbeat = DateTime.UtcNow;

        var newDeployId = Guid.NewGuid();
        _messaging.RuntimePipe.Send<DeployIdResponse>(Arg.Any<IRuntimePipeId>(), Arg.Any<object>())
                  .Returns(_ => new DeployIdResponse { DeployId = newDeployId });
        _deployContext.Set(newDeployId, Arg.Any<IReadOnlyLifetime>())
                      .Returns(Task.FromException(new InvalidOperationException("subscriber failed")));

        var act = () => CreateChecker(GameRoles.Meta).Poll(new Lifetime());

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("subscriber failed");
    }

    [Fact]
    public async Task EpochSwitch_DoesNotOpenConnections()
    {
        _lastHeartbeat = DateTime.UtcNow;

        var newDeployId = Guid.NewGuid();
        _messaging.RuntimePipe.Send<DeployIdResponse>(Arg.Any<IRuntimePipeId>(), Arg.Any<object>())
                  .Returns(_ => new DeployIdResponse { DeployId = newDeployId });

        await CreateChecker(GameRoles.Meta).Poll(new Lifetime());

        await _deployContext.Received(1).Set(newDeployId, Arg.Any<IReadOnlyLifetime>());
        await _clusterFeatures.DidNotReceive().SetAcceptingConnections(Arg.Any<bool>());
    }

    private DeployHealthChecker CreateChecker(ServiceTag tag)
    {
        var self = Substitute.For<IServiceOverview>();
        self.Tag.Returns(tag);

        var discovery = Substitute.For<IServiceDiscovery>();
        discovery.Self.Returns(self);

        var orleans = Substitute.For<IOrleans>();
        orleans.Client.GetGrain<IDeployManagement>(Arg.Any<Guid>(), Arg.Any<string?>()).Returns(_deployGrain);

        var options = Options.Create(new CoordinatorHealthOptions { StaleThreshold = TimeSpan.FromSeconds(15) });

        return new DeployHealthChecker(
            discovery,
            _deployContext,
            _messaging,
            orleans,
            _clusterFeatures,
            options,
            NullLogger<DeployHealthChecker>.Instance);
    }
}
