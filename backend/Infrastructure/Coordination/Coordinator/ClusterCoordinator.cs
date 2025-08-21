using Features;

namespace Infrastructure.Coordination;

public class ClusterCoordinator : BackgroundService
{
    public ClusterCoordinator(IClusterFeatures clusterFeatures, ILogger<ClusterCoordinator> logger)
    {
        _clusterFeatures = clusterFeatures;
        _logger = logger;
    }

    private readonly IClusterFeatures _clusterFeatures;
    private readonly ILogger<ClusterCoordinator> _logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[Coordinator] Starting cluster coordinator...");
        
        await _clusterFeatures.SetAcceptingConnections(false);

        
        await _clusterFeatures.SetAcceptingConnections(true);
        
        _logger.LogInformation("[Coordinator] Cluster coordinator finished");
    }
}