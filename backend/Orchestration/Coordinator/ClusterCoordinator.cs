using Cluster.Deploy;
using Cluster.State;
using Common.Reactive;
using Infrastructure;

namespace Coordinator;

public class ClusterCoordinator : ILocalSetupCompleted, IDeployAware
{
    public ClusterCoordinator(
        IOrleans orleans,
        IDeployContext deployContext,
        IClusterFeatures clusterFeatures,
        ISideEffectsStorage sideEffectsStorage,
        ILogger<ClusterCoordinator> logger)
    {
        _orleans = orleans;
        _deployContext = deployContext;
        _clusterFeatures = clusterFeatures;
        _sideEffectsStorage = sideEffectsStorage;
        _logger = logger;
    }

    private readonly IOrleans _orleans;
    private readonly IDeployContext _deployContext;
    private readonly IClusterFeatures _clusterFeatures;
    private readonly ISideEffectsStorage _sideEffectsStorage;
    private readonly ILogger<ClusterCoordinator> _logger;

    public async Task OnLocalSetupCompleted(IReadOnlyLifetime lifetime)
    {
        await RunCoordinatorSetup(lifetime);
    }

    public async Task OnDeployChanged(Guid newDeployId, IReadOnlyLifetime deployLifetime)
    {
        _logger.LogInformation("[Coordinator] Deploy changed to {DeployId}, re-running setup", newDeployId);
        await RunCoordinatorSetup(deployLifetime);
    }

    private async Task RunCoordinatorSetup(IReadOnlyLifetime lifetime)
    {
        _logger.LogInformation("[Coordinator] Starting cluster coordinator...");

        await _clusterFeatures.SetAcceptingConnections(false);

        _logger.LogInformation("[Coordinator] Requeueing stuck side effects...");
        await _sideEffectsStorage.RequeueStuck();

        await _clusterFeatures.SetAcceptingConnections(true);
        await _clusterFeatures.SetMatchmakingEnabled(true);
        await _clusterFeatures.SetSideEffectsEnabled(true);

        _logger.LogInformation("[Coordinator] Cluster coordinator finished");

        var grain = _orleans.GetGrain<IDeployManagement>(_deployContext.DeployId);
        await grain.MarkCoordinatorReady();
    }
}
