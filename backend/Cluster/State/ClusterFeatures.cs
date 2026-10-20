using Cluster.Deploy;
using Common;
using Common.Extensions;
using Common.Reactive;
using Infrastructure;
using Infrastructure.State;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared;

namespace Cluster.State;


public interface IClusterFeatures : IDeploymentState<ClusterFeaturesState>, IClusterFlags
{
    // Operator / coordinator switch combined with the coordinator health seen by the gateways.
    bool IsAcceptingConnections { get; }

    Task SetAcceptingConnections(bool accepting);
    Task SetMatchmakingEnabled(bool enabled);
    Task SetSideEffectsEnabled(bool enabled);
    Task SetSnapshotDiffGuardEnabled(bool enabled);
    Task SetCoordinatorUnhealthy(bool unhealthy);
}

public class ClusterFeatures : DeploymentState<ClusterFeaturesState>, IClusterFeatures
{
    public ClusterFeatures(
        IOrleans orleans,
        IMessaging messaging,
        ILoggerFactory loggerFactory)
        : base(orleans, messaging, loggerFactory, new ClusterFeaturesState())
    {
    }

    bool IClusterFlags.MatchmakingEnabled => Value.MatchmakingEnabled;
    bool IClusterFlags.SideEffectsEnabled => Value.SideEffectsEnabled;
    bool IClusterFlags.SnapshotDiffGuardEnabled => Value.SnapshotDiffGuardEnabled;

    public bool IsAcceptingConnections => Value.AcceptingConnections && Value.CoordinatorUnhealthy == false;

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
            "AcceptingConnections={Accepting}, Matchmaking={Matchmaking}, SideEffects={SideEffects}, " +
            "SnapshotDiffGuard={SnapshotDiffGuard}, CoordinatorUnhealthy={CoordinatorUnhealthy}",
            newDeployId,
            Value.AcceptingConnections,
            Value.MatchmakingEnabled,
            Value.SideEffectsEnabled,
            Value.SnapshotDiffGuardEnabled,
            Value.CoordinatorUnhealthy);
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
            "AcceptingConnections={Accepting}, Matchmaking={Matchmaking}, SideEffects={SideEffects}, " +
            "SnapshotDiffGuard={SnapshotDiffGuard}, CoordinatorUnhealthy={CoordinatorUnhealthy}",
            _deployId,
            Value.AcceptingConnections,
            Value.MatchmakingEnabled,
            Value.SideEffectsEnabled,
            Value.SnapshotDiffGuardEnabled,
            Value.CoordinatorUnhealthy);
    }

    public Task SetAcceptingConnections(bool accepting) =>
        Apply(new ClusterFeaturesPatch { AcceptingConnections = accepting });

    public Task SetMatchmakingEnabled(bool enabled) =>
        Apply(new ClusterFeaturesPatch { MatchmakingEnabled = enabled });

    public Task SetSideEffectsEnabled(bool enabled) =>
        Apply(new ClusterFeaturesPatch { SideEffectsEnabled = enabled });

    public Task SetSnapshotDiffGuardEnabled(bool enabled) =>
        Apply(new ClusterFeaturesPatch { SnapshotDiffGuardEnabled = enabled });

    public Task SetCoordinatorUnhealthy(bool unhealthy) =>
        Apply(new ClusterFeaturesPatch { CoordinatorUnhealthy = unhealthy });

    private async Task Apply(ClusterFeaturesPatch patch)
    {
        if (IsInitialized == false)
            throw new InvalidOperationException("Cluster features not attached to a deploy yet");

        var deployId = _deployId;

        _logger.LogInformation("[ClusterFeatures] Applying {Patch} for deploy {DeployId}", patch, deployId);

        var state = await _orleans.GetGrain<IClusterFeaturesWriter>(deployId).Apply(patch);

        // The epoch changed while the write was in flight: the result belongs to the old deploy row.
        if (_deployId != deployId)
            return;

        // The same state also comes back through the channel; whichever arrives first is applied.
        TryApplyUpdate(state);
    }
}

[GenerateSerializer]
public class ClusterFeaturesPatch
{
    [Id(0)] public bool? AcceptingConnections { get; set; }
    [Id(1)] public bool? MatchmakingEnabled { get; set; }
    [Id(2)] public bool? SideEffectsEnabled { get; set; }
    [Id(3)] public bool? SnapshotDiffGuardEnabled { get; set; }
    [Id(4)] public bool? CoordinatorUnhealthy { get; set; }

    public void ApplyTo(ClusterFeaturesState state)
    {
        if (AcceptingConnections is { } accepting)
            state.AcceptingConnections = accepting;

        if (MatchmakingEnabled is { } matchmaking)
            state.MatchmakingEnabled = matchmaking;

        if (SideEffectsEnabled is { } sideEffects)
            state.SideEffectsEnabled = sideEffects;

        if (SnapshotDiffGuardEnabled is { } snapshotDiffGuard)
            state.SnapshotDiffGuardEnabled = snapshotDiffGuard;

        if (CoordinatorUnhealthy is { } unhealthy)
            state.CoordinatorUnhealthy = unhealthy;
    }

    public override string ToString()
    {
        return $"AcceptingConnections={AcceptingConnections?.ToString() ?? "-"}, " +
               $"Matchmaking={MatchmakingEnabled?.ToString() ?? "-"}, " +
               $"SideEffects={SideEffectsEnabled?.ToString() ?? "-"}, " +
               $"SnapshotDiffGuard={SnapshotDiffGuardEnabled?.ToString() ?? "-"}, " +
               $"CoordinatorUnhealthy={CoordinatorUnhealthy?.ToString() ?? "-"}";
    }
}

public interface IClusterFeaturesWriter : IGrainWithGuidKey
{
    Task<AddressableStateValue> Apply(ClusterFeaturesPatch patch);
}

// Keyed by deploy id. A non-reentrant grain serializes read-modify-write of the deploy row across all
// services: concurrent patches of different flags no longer overwrite each other.
public class ClusterFeaturesWriter : Grain, IClusterFeaturesWriter
{
    public ClusterFeaturesWriter(IOrleans orleans, IMessaging messaging)
    {
        _orleans = orleans;
        _messaging = messaging;
    }

    private readonly IOrleans _orleans;
    private readonly IMessaging _messaging;

    public static StateIdentity CreateStateIdentity(IOrleans orleans, Guid deployId)
    {
        var stateInfo = orleans.StateStorage.Registry.Get<ClusterFeaturesState>();

        return new StateIdentity
        {
            Key = deployId,
            Type = stateInfo.Name,
            TableName = stateInfo.TableName,
            Extension = null
        };
    }

    public async Task<AddressableStateValue> Apply(ClusterFeaturesPatch patch)
    {
        var deployId = this.GetPrimaryKey();
        var identity = CreateStateIdentity(_orleans, deployId);

        // Read the row on every call: the activation may have moved to another silo since the last write.
        var stored = await _orleans.StateStorage.Read<AddressableStateValue>(identity);

        var value = stored.IsInitialized
            ? _orleans.Serializer.Deserialize<ClusterFeaturesState>(stored.Value)
            : new ClusterFeaturesState();

        patch.ApplyTo(value);

        // Readers drop updates not newer than the last applied one: keep UpdateDate strictly growing
        // even if this silo's clock is behind the one that wrote the previous value.
        var now = DateTime.UtcNow;

        var next = new AddressableStateValue
        {
            IsInitialized = true,
            UpdateDate = now > stored.UpdateDate ? now : stored.UpdateDate.AddTicks(1),
            Value = _orleans.Serializer.Serialize(value)
        };

        await _orleans.StateStorage.Write(identity, next);
        await _messaging.PublishChannel(new DeploymentStateChannelId<ClusterFeaturesState>(deployId), next);

        return next;
    }
}

[GenerateSerializer]
[GrainState(Table = "cluster", State = "cluster_features", Lookup = "ClusterFeatures", Key = GrainKeyType.Guid)]
public class ClusterFeaturesState : IDirectStateValue, IDeployScopedState
{
    [Id(0)] public bool AcceptingConnections { get; set; } = false;
    [Id(1)] public bool MatchmakingEnabled { get; set; } = false;
    [Id(2)] public bool SideEffectsEnabled { get; set; } = false;
    [Id(3)] public bool SnapshotDiffGuardEnabled { get; set; } = false;

    // Set by gateways while the coordinator heartbeat is stale, cleared by any service once it is fresh
    // again. Kept apart from AcceptingConnections so that recovery does not override the operator switch.
    [Id(4)] public bool CoordinatorUnhealthy { get; set; } = false;

    public int Version => 0;
}

public static class ClusterFeaturesExtensions
{
    public static IHostApplicationBuilder AddClusterFeatures(this IHostApplicationBuilder builder)
    {
        builder.Add<ClusterFeatures>()
               .As<IClusterFeatures>()
               .As<IDeployAware>();

        builder.Services.AddSingleton<IClusterFlags>(sp => sp.GetRequiredService<IClusterFeatures>());

        return builder;
    }
}