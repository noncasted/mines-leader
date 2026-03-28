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
        ISideEffectsStorage sideEffectsStorage,
        ILogger<ClusterCoordinator> logger)
    {
        _messaging = messaging;
        _clusterFeatures = clusterFeatures;
        _sideEffectsStorage = sideEffectsStorage;
        _logger = logger;
    }

    private readonly IMessaging _messaging;
    private readonly IClusterFeatures _clusterFeatures;
    private readonly ISideEffectsStorage _sideEffectsStorage;
    private readonly ILogger<ClusterCoordinator> _logger;

    public async Task OnLocalSetupCompleted(IReadOnlyLifetime lifetime)
    {
        _logger.LogInformation("[Coordinator] Starting cluster coordinator...");

        await _clusterFeatures.SetAcceptingConnections(false);

        _logger.LogInformation("[Coordinator] Requeueing stuck side effects...");
        await _sideEffectsStorage.RequeueStuck();

        await _clusterFeatures.SetAcceptingConnections(true);
        await _clusterFeatures.SetMatchmakingEnabled(true);
        await _clusterFeatures.SetSideEffectsEnabled(true);
        
        _logger.LogInformation("[Coordinator] Cluster coordinator finished");

        await _messaging.PublishChannel(CoordinatorEvents.ReadyId, new CoordinatorEvents.ReadyPayload());
    }
}