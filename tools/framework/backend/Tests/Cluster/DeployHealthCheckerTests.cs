using Cluster.Deploy;
using Cluster.Discovery;
using Cluster.State;
using Internal;
using FluentAssertions;
using Framework.Tests.Fixtures;
using Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Framework.Tests.Cluster;

public class DeployHealthCheckerTests : LifetimeTest
{
    private readonly Guid _deployId = Guid.NewGuid();
    private readonly ClusterFeaturesState _features = new();
    private readonly IClusterFeatures _clusterFeatures = Substitute.For<IClusterFeatures>();
    private readonly IDeployManagement _deployGrain = Substitute.For<IDeployManagement>();
    private readonly IDeployContext _deployContext = Substitute.For<IDeployContext>();
    private readonly IMessaging _messaging = Substitute.For<IMessaging>();
    private readonly Dictionary<Guid, DateTime> _createdAt = new();
    private DateTime _lastHeartbeat;

    public DeployHealthCheckerTests()
    {
        _features.SetAcceptingConnections(true);
        _clusterFeatures.Value.Returns(_ => _features);
        _clusterFeatures.SetCoordinatorUnhealthy(Arg.Any<bool>()).Returns(call => {
            _features.SetCoordinatorUnhealthy(call.Arg<bool>());
            return Task.CompletedTask;
        });

        _createdAt[_deployId] = DateTime.UtcNow - TimeSpan.FromMinutes(10);

        _deployGrain.GetState().Returns(_ => {
            var state = new DeployState();
            state.Init(_deployId, _createdAt[_deployId]);
            state.MarkCoordinatorReady(_lastHeartbeat);
            return state;
        });

        _deployContext.DeployId.Returns(_ => _deployId);

        _messaging.RuntimePipe.Send<DeployIdResponse>(Arg.Any<IRuntimePipeId>(), Arg.Any<object>())
                  .Returns(_ => new DeployIdResponse { DeployId = _deployId });
    }

    [Fact]
    public async Task StaleHeartbeat_ClosesDeploy_FreshHeartbeat_ReopensIt()
    {
        var checker = CreateChecker(TestRoles.Meta);

        _lastHeartbeat = DateTime.UtcNow - TimeSpan.FromMinutes(1);
        await checker.Poll(TestLifetime);

        _features.CoordinatorUnhealthy.Should().BeTrue();

        _lastHeartbeat = DateTime.UtcNow;
        await checker.Poll(TestLifetime);

        _features.CoordinatorUnhealthy.Should().BeFalse();
        _features.AcceptingConnections.Should().BeTrue("recovery must not touch the operator switch");
        await _clusterFeatures.DidNotReceive().SetAcceptingConnections(Arg.Any<bool>());
    }

    [Fact]
    public async Task FreshHeartbeat_ReopensDeploy_ClosedByAnotherService()
    {
        _features.SetCoordinatorUnhealthy(true);
        _lastHeartbeat = DateTime.UtcNow;

        // The coordinator never closes the deploy itself, but reopens it like everyone else.
        await CreateChecker(ServiceTag.Coordinator).Poll(TestLifetime);

        _features.CoordinatorUnhealthy.Should().BeFalse();
    }

    [Fact]
    public async Task FreshHeartbeat_HealthyDeploy_WritesNothing()
    {
        _lastHeartbeat = DateTime.UtcNow;

        await CreateChecker(TestRoles.Game).Poll(TestLifetime);

        await _clusterFeatures.DidNotReceive().SetCoordinatorUnhealthy(Arg.Any<bool>());
    }

    [Fact]
    public async Task StaleHeartbeat_SeenByCoordinator_DoesNotCloseDeploy()
    {
        _lastHeartbeat = DateTime.UtcNow - TimeSpan.FromMinutes(1);

        await CreateChecker(ServiceTag.Coordinator).Poll(TestLifetime);

        await _clusterFeatures.DidNotReceive().SetCoordinatorUnhealthy(Arg.Any<bool>());
    }

    [Fact]
    public async Task EpochSwitch_Fails_Propagates()
    {
        _lastHeartbeat = DateTime.UtcNow;

        var newDeployId = AddDeploy(TimeSpan.FromMinutes(1));
        _messaging.RuntimePipe.Send<DeployIdResponse>(Arg.Any<IRuntimePipeId>(), Arg.Any<object>())
                  .Returns(_ => new DeployIdResponse { DeployId = newDeployId });
        _deployContext.Set(newDeployId, Arg.Any<IReadOnlyLifetime>())
                      .Returns(Task.FromException(new InvalidOperationException("subscriber failed")));

        var act = () => CreateChecker(TestRoles.Meta).Poll(TestLifetime);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("subscriber failed");
    }

    [Fact]
    public async Task EpochSwitch_DoesNotOpenConnections()
    {
        _lastHeartbeat = DateTime.UtcNow;

        var newDeployId = AddDeploy(TimeSpan.FromMinutes(1));
        _messaging.RuntimePipe.Send<DeployIdResponse>(Arg.Any<IRuntimePipeId>(), Arg.Any<object>())
                  .Returns(_ => new DeployIdResponse { DeployId = newDeployId });

        await CreateChecker(TestRoles.Meta).Poll(TestLifetime);

        await _deployContext.Received(1).Set(newDeployId, Arg.Any<IReadOnlyLifetime>());
        await _clusterFeatures.DidNotReceive().SetAcceptingConnections(Arg.Any<bool>());
    }

    [Fact]
    public async Task CoordinatorReplaced_LeavesItsDeploy()
    {
        _lastHeartbeat = DateTime.UtcNow;

        _messaging.RuntimePipe.Send<DeployIdResponse>(Arg.Any<IRuntimePipeId>(), Arg.Any<object>())
                  .Returns(_ => new DeployIdResponse { DeployId = AddDeploy(TimeSpan.FromMinutes(1)) });

        await CreateChecker(ServiceTag.Coordinator).Poll(TestLifetime);

        await _clusterFeatures.Received(1).SetAcceptingConnections(false);
        _deployContext.Received(1).Leave();
        await _deployContext.DidNotReceive().Set(Arg.Any<Guid>(), Arg.Any<IReadOnlyLifetime>());
    }

    [Fact]
    public async Task ReplacedCoordinatorAnswersPipe_NewerCoordinatorStaysInItsDeploy()
    {
        _lastHeartbeat = DateTime.UtcNow;

        // The pipe is answered by the last bind: the replaced coordinator keeps rebinding its handler, so the
        // answers alternate between the old deploy and the deploy of this coordinator.
        var olderDeployId = AddDeploy(TimeSpan.FromMinutes(30));
        var answers = 0;
        _messaging.RuntimePipe.Send<DeployIdResponse>(Arg.Any<IRuntimePipeId>(), Arg.Any<object>())
                  .Returns(_ => new DeployIdResponse { DeployId = answers++ % 2 == 0 ? olderDeployId : _deployId });

        var checker = CreateChecker(ServiceTag.Coordinator);

        for (var i = 0; i < 4; i++)
            await checker.Poll(TestLifetime);

        _deployContext.DidNotReceive().Leave();
        await _clusterFeatures.DidNotReceive().SetAcceptingConnections(Arg.Any<bool>());
    }

    [Fact]
    public async Task ReplacedCoordinatorAnswersPipe_ServiceDoesNotSwitchToOlderDeploy()
    {
        _lastHeartbeat = DateTime.UtcNow;

        var olderDeployId = AddDeploy(TimeSpan.FromMinutes(30));
        _messaging.RuntimePipe.Send<DeployIdResponse>(Arg.Any<IRuntimePipeId>(), Arg.Any<object>())
                  .Returns(_ => new DeployIdResponse { DeployId = olderDeployId });

        await CreateChecker(TestRoles.Meta).Poll(TestLifetime);

        await _deployContext.DidNotReceive().Set(Arg.Any<Guid>(), Arg.Any<IReadOnlyLifetime>());
    }

    private Guid AddDeploy(TimeSpan age)
    {
        var deployId = Guid.NewGuid();
        _createdAt[deployId] = DateTime.UtcNow - age;
        return deployId;
    }

    private IDeployManagement DeployGrain(Guid deployId)
    {
        if (deployId == _deployId)
            return _deployGrain;

        var grain = Substitute.For<IDeployManagement>();
        grain.GetState().Returns(_ => {
            var state = new DeployState();
            state.Init(deployId, _createdAt[deployId]);
            state.MarkCoordinatorReady(DateTime.UtcNow);
            return state;
        });
        return grain;
    }

    private DeployHealthChecker CreateChecker(ServiceTag tag)
    {
        var self = Substitute.For<IServiceOverview>();
        self.Tag.Returns(tag);

        var discovery = Substitute.For<IServiceDiscovery>();
        discovery.Self.Returns(self);

        var orleans = Substitute.For<IOrleans>();
        orleans.Client.GetGrain<IDeployManagement>(Arg.Any<Guid>(), Arg.Any<string?>())
               .Returns(call => DeployGrain(call.ArgAt<Guid>(0)));

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
