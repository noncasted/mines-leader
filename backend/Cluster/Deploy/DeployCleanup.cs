using Common.Extensions;
using Common.Reactive;
using Infrastructure.State;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared;

namespace Cluster.Deploy;

public interface IDeployCleanup
{
    Task<DeployCleanupResult> Run();
}

public class DeployCleanupResult
{
    public required int Removed { get; init; }
    public required IReadOnlyList<Guid> RemovedDeployIds { get; init; }
    public required Guid KeptDeployId { get; init; }
}

public class DeployCleanup : IDeployCleanup, IHostedService
{
    public DeployCleanup(
        IDeployContext deployContext,
        IStateStorage stateStorage,
        ILogger<DeployCleanup> logger)
    {
        _deployContext = deployContext;
        _stateStorage = stateStorage;
        _logger = logger;
    }

    private readonly IDeployContext _deployContext;
    private readonly IStateStorage _stateStorage;
    private readonly ILogger<DeployCleanup> _logger;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public async Task<DeployCleanupResult> Run()
    {
        var currentDeployId = _deployContext.DeployId;

        if (currentDeployId == Guid.Empty)
            throw new InvalidOperationException("No active deploy, refusing to run cleanup");

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

public static class DeployCleanupExtensions
{
    public static IHostApplicationBuilder AddDeployCleanup(this IHostApplicationBuilder builder)
    {
        builder.Add<DeployCleanup>()
               .As<IDeployCleanup>()
               .As<IHostedService>();

        return builder;
    }
}
