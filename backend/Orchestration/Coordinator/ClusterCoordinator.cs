using Cluster.Coordination;
using Cluster.State;
using Common.Reactive;
using Infrastructure;

namespace Coordinator;

public class ClusterCoordinator : ILocalSetupCompleted
{
    public ClusterCoordinator(
        IMessaging messaging,
        IClusterFeatures clusterFeatures,
        BatchWritersWakeUp batchWritersWakeUp,
        StatesSetup statesSetup,
        ILogger<ClusterCoordinator> logger)
    {
        _messaging = messaging;
        _clusterFeatures = clusterFeatures;
        _batchWritersWakeUp = batchWritersWakeUp;
        _statesSetup = statesSetup;
        _logger = logger;
    }

    private readonly IMessaging _messaging;
    private readonly IClusterFeatures _clusterFeatures;
    private readonly BatchWritersWakeUp _batchWritersWakeUp;
    private readonly StatesSetup _statesSetup;
    private readonly ILogger<ClusterCoordinator> _logger;

    public async Task OnLocalSetupCompleted(IReadOnlyLifetime lifetime)
    {
        _logger.LogInformation("[Coordinator] Starting cluster coordinator...");
        
        await _clusterFeatures.SetAcceptingConnections(false);

        await _batchWritersWakeUp.Execute();
        await _statesSetup.Run();
        
        await _clusterFeatures.SetAcceptingConnections(true);
        
        _logger.LogInformation("[Coordinator] Cluster coordinator finished");

        await _messaging.PushDirectQueue(CoordinatorEvents.ReadyId, new CoordinatorEvents.ReadyPayload());
    }
}