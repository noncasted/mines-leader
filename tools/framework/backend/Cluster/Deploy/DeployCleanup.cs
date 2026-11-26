using Common.Extensions;
using Internal;
using Infrastructure;
using Infrastructure.State;
using Microsoft.Extensions.Logging;
using Shared;

namespace Cluster.Deploy;

public interface IDeployCleanup
{
    Task<DeployCleanupResult> Run();
}

public class DeployCleanup : IDeployCleanup
{
    public DeployCleanup(
        IDeployContext deployContext,
        IStateStorage stateStorage,
        IMessaging messaging,
        ILogger<DeployCleanup> logger)
    {
        _deployContext = deployContext;
        _stateStorage = stateStorage;
        _messaging = messaging;
        _logger = logger;
    }

    private readonly IDeployContext _deployContext;
    private readonly IStateStorage _stateStorage;
    private readonly IMessaging _messaging;
    private readonly ILogger<DeployCleanup> _logger;

    public async Task<DeployCleanupResult> Run()
    {
        var currentDeployId = _deployContext.DeployId;

        if (currentDeployId == Guid.Empty)
            throw new InvalidOperationException("No active deploy, refusing to run cleanup");

        // The local deploy id of a host started before the current deploy (a lagging console) is an old one: the rows
        // of the deploy the cluster runs would be taken for stale. The coordinator answers the current one.
        var clusterDeploy = await _messaging.SendPipe<DeployIdResponse>(DeployIdPipe.Id, new DeployIdRequest());

        if (clusterDeploy.DeployId != currentDeployId)
        {
            throw new InvalidOperationException(
                $"Local deploy {currentDeployId} is not the current deploy {clusterDeploy.DeployId} of the cluster, refusing to run cleanup");
        }

        var lifetime = new Lifetime();

        try
        {
            var toDelete = new List<StateIdentity>();
            var staleIds = new HashSet<Guid>();

            // Keys only: the rows of DeploymentState hold an AddressableStateValue under the state name,
            // not the registered type, so reading the values would fail.
            foreach (var info in DeployScopedStates(_stateStorage.Registry))
            {
                await foreach (var key in _stateStorage.ReadAllKeys<Guid>(info, lifetime))
                {
                    if (key == currentDeployId)
                        continue;

                    toDelete.Add(new StateIdentity
                    {
                        Key = key,
                        Type = info.Name,
                        TableName = info.TableName,
                        Extension = null
                    });

                    staleIds.Add(key);
                }
            }

            if (toDelete.Count > 0)
            {
                await _stateStorage.Delete(new StateDeleteRequest { Identities = toDelete });

                _logger.LogInformation(
                    "[DeployCleanup] Removed {Count} stale deploy records across {DeployCount} deploys, kept {Current}",
                    toDelete.Count, staleIds.Count, currentDeployId);
            }
            else
            {
                _logger.LogInformation("[DeployCleanup] No stale deploy records to remove");
            }

            return new DeployCleanupResult
            {
                Removed = toDelete.Count,
                RemovedDeployIds = staleIds.ToList(),
                KeptDeployId = currentDeployId
            };
        }
        finally
        {
            lifetime.Terminate();
        }
    }

    // The registered states marked with IDeployScopedState, in a stable order.
    public static IReadOnlyList<GrainStateInfo> DeployScopedStates(IGrainStatesRegistry registry)
    {
        var result = registry.All
                             .Where(info => typeof(IDeployScopedState).IsAssignableFrom(info.Type))
                             .OrderBy(info => info.Type.FullName, StringComparer.Ordinal)
                             .ToList();

        foreach (var info in result)
        {
            if (info.KeyType != GrainKeyType.Guid)
                throw new InvalidOperationException(
                    $"Deploy-scoped state {info.Type.FullName} must be keyed by the deploy id (Guid), got {info.KeyType}");

            if (typeof(IDirectStateValue).IsAssignableFrom(info.Type) == false)
                throw new InvalidOperationException(
                    $"Deploy-scoped state {info.Type.FullName} must be a direct state (IDirectStateValue)");
        }

        return result;
    }
}
