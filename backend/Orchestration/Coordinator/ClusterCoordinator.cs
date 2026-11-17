using Cluster.Deploy;
using Cluster.State;
using Internal;
using Infrastructure;

namespace Coordinator;

// Runs once per Coordinator process, after its own deploy id is set (ClusterParticipantStartup "Local Setup" stage).
// Not IDeployAware: DeployIdentity generates the deploy id once per process start and a new epoch means a new
// process, so the local setup stage already covers every epoch.
public class ClusterCoordinator : ILocalSetupCompleted
{
    public ClusterCoordinator(
        IOrleans orleans,
        IDeployContext deployContext,
        IClusterFeatures clusterFeatures,
        ILogger<ClusterCoordinator> logger)
    {
        _orleans = orleans;
        _deployContext = deployContext;
        _clusterFeatures = clusterFeatures;
        _logger = logger;
    }

    private readonly IOrleans _orleans;
    private readonly IDeployContext _deployContext;
    private readonly IClusterFeatures _clusterFeatures;
    private readonly ILogger<ClusterCoordinator> _logger;

    public async Task OnLocalSetupCompleted(IReadOnlyLifetime lifetime)
    {
        _logger.LogInformation("[Coordinator] Starting cluster coordinator...");

        await _clusterFeatures.SetAcceptingConnections(false);

        await _clusterFeatures.SetAcceptingConnections(true);
        await _clusterFeatures.SetGameFlag(GameFlagNames.Matchmaking, true);
        await _clusterFeatures.SetSideEffectsEnabled(true);

        _logger.LogInformation("[Coordinator] Cluster coordinator finished");

        var grain = _orleans.GetGrain<IDeployManagement>(_deployContext.DeployId);
        await grain.MarkCoordinatorReady();
    }
}
