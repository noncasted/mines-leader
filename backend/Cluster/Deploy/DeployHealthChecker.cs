using Cluster.Discovery;
using Cluster.State;
using Common.Reactive;
using Infrastructure;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cluster.Deploy;

public class DeployHealthChecker : BackgroundService
{
    public DeployHealthChecker(
        IServiceDiscovery discovery,
        IDeployContext deployContext,
        IMessaging messaging,
        IOrleans orleans,
        IClusterFeatures clusterFeatures,
        IOptions<CoordinatorHealthOptions> options,
        ILogger<DeployHealthChecker> logger)
    {
        _discovery = discovery;
        _deployContext = deployContext;
        _messaging = messaging;
        _orleans = orleans;
        _clusterFeatures = clusterFeatures;
        _options = options.Value;
        _logger = logger;
    }

    private readonly IServiceDiscovery _discovery;
    private readonly IDeployContext _deployContext;
    private readonly IMessaging _messaging;
    private readonly IOrleans _orleans;
    private readonly IClusterFeatures _clusterFeatures;
    private readonly CoordinatorHealthOptions _options;
    private readonly ILogger<DeployHealthChecker> _logger;

    protected override async Task ExecuteAsync(CancellationToken cancellation)
    {
        var lifetime = cancellation.ToLifetime();

        while (_deployContext.DeployId == Guid.Empty && lifetime.IsTerminated == false)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(1), cancellation);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }

        while (lifetime.IsTerminated == false)
        {
            try
            {
                await Task.Delay(_options.HealthPollInterval, cancellation);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await Poll(lifetime);
        }
    }

    // One health iteration. Transient Orleans / messaging failures are logged and retried on the next poll;
    // a failed deploy epoch switch propagates and stops the host (BackgroundService failure).
    public async Task Poll(IReadOnlyLifetime lifetime)
    {
        var serviceName = _discovery.Self.Tag.ToString();
        var isCoordinator = _discovery.Self.Tag == ServiceTag.Coordinator;

        await CheckDeployEpoch(lifetime, serviceName, isCoordinator);
        await CheckHeartbeat(serviceName, isCoordinator);
    }

    private async Task CheckDeployEpoch(IReadOnlyLifetime lifetime, string serviceName, bool isCoordinator)
    {
        DeployIdResponse response;

        try
        {
            response = await _messaging.SendPipe<DeployIdResponse>(DeployIdPipe.Id, new DeployIdRequest());
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "[Health] {Service} deploy id poll failed", serviceName);
            return;
        }

        if (response.DeployId == Guid.Empty)
            return;

        if (response.DeployId == _deployContext.DeployId)
            return;

        if (isCoordinator)
        {
            _logger.LogCritical(
                "[Health] Coordinator replaced: new deploy {New}, my deploy {Mine}, going passive",
                response.DeployId, _deployContext.DeployId);

            try
            {
                await _clusterFeatures.SetAcceptingConnections(false);
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "[Health] Replaced coordinator failed to close its deploy for connections");
            }

            return;
        }

        _logger.LogWarning("[Health] {Service} deploy epoch changed {Old} -> {New}, switching",
            serviceName, _deployContext.DeployId, response.DeployId);

        // Not caught: a service that could not attach to the new epoch must crash, not keep running half-switched.
        // AcceptingConnections of the new epoch is opened by its coordinator after its setup.
        await _deployContext.Set(response.DeployId, lifetime);

        try
        {
            await _discovery.Push();
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "[Health] {Service} discovery push after epoch switch failed", serviceName);
        }
    }

    private async Task CheckHeartbeat(string serviceName, bool isCoordinator)
    {
        try
        {
            var grain = _orleans.GetGrain<IDeployManagement>(_deployContext.DeployId);
            var state = await grain.GetState();
            var since = DateTime.UtcNow - state.LastHeartbeat;

            if (since > _options.StaleThreshold)
            {
                _logger.LogCritical(
                    "[Health] {Service} coordinator unhealthy for deploy {DeployId}, last heartbeat {Seconds:F1}s ago. Going passive.",
                    serviceName, _deployContext.DeployId, since.TotalSeconds);

                if (isCoordinator == false && _clusterFeatures.Value.CoordinatorUnhealthy == false)
                    await _clusterFeatures.SetCoordinatorUnhealthy(true);

                return;
            }

            // Any service that sees a fresh heartbeat reopens the deploy, so recovery does not depend on
            // the particular gateway that closed it still being alive.
            if (_clusterFeatures.Value.CoordinatorUnhealthy)
            {
                _logger.LogWarning(
                    "[Health] {Service} coordinator heartbeat for deploy {DeployId} is fresh again ({Seconds:F1}s ago), reopening",
                    serviceName, _deployContext.DeployId, since.TotalSeconds);

                await _clusterFeatures.SetCoordinatorUnhealthy(false);
            }
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "[Health] {Service} heartbeat check failed", serviceName);
        }
    }
}
