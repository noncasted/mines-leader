using Cluster.Deploy;
using Common;
using Common.Extensions;
using Internal;
using Infrastructure;
using Infrastructure.State;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Shared;

namespace Cluster.State;

public class ClusterFeatures : DeploymentState<ClusterFeaturesState>, IClusterFeatures
{
    public ClusterFeatures(
        IOrleans orleans,
        IMessaging messaging,
        ILoggerFactory loggerFactory)
        : base(orleans, messaging, loggerFactory, new ClusterFeaturesState())
    {
    }

    bool IClusterFlags.SideEffectsEnabled => Value.SideEffectsEnabled;

    public bool IsAcceptingConnections => Value.AcceptingConnections && Value.CoordinatorUnhealthy == false;

    public bool IsGameFlagEnabled(string name) => Value.GameFlags.TryGetValue(name, out var enabled) && enabled;

    protected override StateIdentity CreateStateIdentity(Guid deployId)
    {
        return ClusterFeaturesWriter.CreateStateIdentity(_orleans, deployId);
    }

    public override async Task OnDeployChanged(Guid newDeployId, IReadOnlyLifetime deployLifetime)
    {
        _logger.LogInformation(
            "[ClusterFeatures] Loading state for deploy {DeployId}",
            newDeployId);

        await base.OnDeployChanged(newDeployId, deployLifetime);

        _logger.LogInformation(
            "[ClusterFeatures] Loaded state for deploy {DeployId}: " +
            "AcceptingConnections={Accepting}, SideEffects={SideEffects}",
            newDeployId,
            Value.AcceptingConnections,
            Value.SideEffectsEnabled);
    }

    // Whole-value writes from a local (possibly stale) copy lose concurrent updates of other services.
    // Every change goes through ClusterFeaturesWriter, the single cluster-wide writer of the deploy row.
    public override Task SetValue(ClusterFeaturesState value)
    {
        throw new NotSupportedException(
            "ClusterFeatures is changed only through the Set* methods of IClusterFeatures");
    }

    protected override void OnUpdate(AddressableStateValue state)
    {
        _logger.LogInformation(
            "[ClusterFeatures] Received remote update for deploy {DeployId}",
            _deployId);

        if (TryApplyUpdate(state) == false)
        {
            _logger.LogInformation(
                "[ClusterFeatures] Skipped stale remote update for deploy {DeployId} (sent {Sent:O})",
                _deployId, state.UpdateDate);
            return;
        }

        _logger.LogInformation(
            "[ClusterFeatures] Remote update applied for deploy {DeployId}: " +
            "AcceptingConnections={Accepting}, SideEffects={SideEffects}",
            _deployId,
            Value.AcceptingConnections,
            Value.SideEffectsEnabled);
    }

    public Task SetAcceptingConnections(bool accepting) =>
        Apply(new ClusterFeaturesPatch { AcceptingConnections = accepting });

    public Task SetSideEffectsEnabled(bool enabled) =>
        Apply(new ClusterFeaturesPatch { SideEffectsEnabled = enabled });

    public Task SetCoordinatorUnhealthy(bool unhealthy) =>
        Apply(new ClusterFeaturesPatch { CoordinatorUnhealthy = unhealthy });

    public Task SetGameFlag(string name, bool enabled) =>
        Apply(new ClusterFeaturesPatch { GameFlags = new Dictionary<string, bool> { [name] = enabled } });

    private async Task Apply(ClusterFeaturesPatch patch)
    {
        if (IsInitialized == false)
            throw new InvalidOperationException("Cluster features not attached to a deploy yet");

        var deployId = _deployId;

        _logger.LogInformation("[ClusterFeatures] Applying {Patch} for deploy {DeployId}", patch, deployId);

        var state = await _orleans.GetGrain<IClusterFeaturesWriter>(deployId).Apply(patch);

        // The same state also comes back through the channel; whichever arrives first is applied. The result of a write
        // that outlived its epoch belongs to the old deploy row and is dropped.
        TryApplyUpdate(deployId, state);
    }
}
