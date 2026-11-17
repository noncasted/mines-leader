using Cluster.Deploy;
using Infrastructure;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Orchestration;

public class CoordinatorReadyHealthCheck : IHealthCheck
{
    public CoordinatorReadyHealthCheck(
        IDeployContext deployContext,
        IOrleans orleans,
        IOptions<CoordinatorHealthOptions> options)
    {
        _deployContext = deployContext;
        _orleans = orleans;
        _options = options.Value;
    }

    private readonly IDeployContext _deployContext;
    private readonly IOrleans _orleans;
    private readonly CoordinatorHealthOptions _options;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var deployId = _deployContext.DeployId;

        if (deployId == Guid.Empty)
            return HealthCheckResult.Unhealthy("Deploy identity not assigned yet");

        try
        {
            var grain = _orleans.GetGrain<IDeployManagement>(deployId);
            var state = await grain.GetState();

            if (state.CoordinatorReady == false)
                return HealthCheckResult.Unhealthy($"Coordinator not ready for deploy {deployId}");

            var since = DateTime.UtcNow - state.LastHeartbeat;

            if (since > _options.StaleThreshold)
                return HealthCheckResult.Unhealthy(
                    $"Coordinator heartbeat stale for deploy {deployId}, last heartbeat {since.TotalSeconds:F1}s ago");

            return HealthCheckResult.Healthy($"Deploy {deployId} ready, heartbeat {since.TotalSeconds:F1}s ago");
        }
        catch (Exception e)
        {
            return HealthCheckResult.Unhealthy(
                $"Failed to check coordinator state for deploy {deployId}", e);
        }
    }
}
