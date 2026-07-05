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
    Task SetAcceptingConnections(bool accepting);
    Task SetMatchmakingEnabled(bool enabled);
    Task SetSideEffectsEnabled(bool enabled);
    Task SetSnapshotDiffGuardEnabled(bool enabled);
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

    protected override StateIdentity CreateStateIdentity(Guid deployId)
    {
        var stateInfo = _orleans.StateStorage.Registry.Get<ClusterFeaturesState>();
        return new StateIdentity
        {
            Key = deployId,
            Type = stateInfo.Name,
            TableName = stateInfo.TableName,
            Extension = null
        };
    }

    public override async Task OnDeployChanged(Guid newDeployId, IReadOnlyLifetime deployLifetime)
    {
        _logger.LogInformation(
            "[ClusterFeatures] Loading state for deploy {DeployId}",
            newDeployId);

        await base.OnDeployChanged(newDeployId, deployLifetime);

        if (IsInitialized)
        {
            _logger.LogInformation(
                "[ClusterFeatures] Loaded state for deploy {DeployId}: " +
                "AcceptingConnections={Accepting}, Matchmaking={Matchmaking}, SideEffects={SideEffects}, SnapshotDiffGuard={SnapshotDiffGuard}",
                newDeployId,
                Value.AcceptingConnections,
                Value.MatchmakingEnabled,
                Value.SideEffectsEnabled,
                Value.SnapshotDiffGuardEnabled);
        }
        else
        {
            _logger.LogWarning(
                "[ClusterFeatures] Failed to load state for deploy {DeployId}, using defaults",
                newDeployId);
        }
    }

    public override async Task SetValue(ClusterFeaturesState value)
    {
        _logger.LogInformation(
            "[ClusterFeatures] Applying update for deploy {DeployId}: " +
            "AcceptingConnections={Accepting}, Matchmaking={Matchmaking}, SideEffects={SideEffects}, SnapshotDiffGuard={SnapshotDiffGuard}",
            _deployId,
            value.AcceptingConnections,
            value.MatchmakingEnabled,
            value.SideEffectsEnabled,
            value.SnapshotDiffGuardEnabled);

        await base.SetValue(value);

        _logger.LogInformation(
            "[ClusterFeatures] State persisted and published for deploy {DeployId}",
            _deployId);
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
            "AcceptingConnections={Accepting}, Matchmaking={Matchmaking}, SideEffects={SideEffects}, SnapshotDiffGuard={SnapshotDiffGuard}",
            _deployId,
            Value.AcceptingConnections,
            Value.MatchmakingEnabled,
            Value.SideEffectsEnabled,
            Value.SnapshotDiffGuardEnabled);
    }

    public Task SetAcceptingConnections(bool accepting) => Apply(s => s.AcceptingConnections = accepting);
    public Task SetMatchmakingEnabled(bool enabled) => Apply(s => s.MatchmakingEnabled = enabled);
    public Task SetSideEffectsEnabled(bool enabled) => Apply(s => s.SideEffectsEnabled = enabled);
    public Task SetSnapshotDiffGuardEnabled(bool enabled) => Apply(s => s.SnapshotDiffGuardEnabled = enabled);

    private async Task Apply(Action<ClusterFeaturesState> mutator)
    {
        if (IsInitialized == false)
            throw new InvalidOperationException("Cluster features not attached to a deploy yet");

        var next = new ClusterFeaturesState
        {
            AcceptingConnections = Value.AcceptingConnections,
            MatchmakingEnabled = Value.MatchmakingEnabled,
            SideEffectsEnabled = Value.SideEffectsEnabled,
            SnapshotDiffGuardEnabled = Value.SnapshotDiffGuardEnabled
        };

        mutator(next);
        await SetValue(next);
    }
}

[GenerateSerializer]
[GrainState(Table = "cluster", State = "cluster_features", Lookup = "ClusterFeatures", Key = GrainKeyType.Guid)]
public class ClusterFeaturesState : IDirectStateValue
{
    [Id(0)] public bool AcceptingConnections { get; set; } = false;
    [Id(1)] public bool MatchmakingEnabled { get; set; } = false;
    [Id(2)] public bool SideEffectsEnabled { get; set; } = false;
    [Id(3)] public bool SnapshotDiffGuardEnabled { get; set; } = false;

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

public static class ClusterFeaturesCompatExtensions
{
    [Obsolete("ClusterFeatures no longer uses a grain. Use IClusterFeatures directly.")]
    public static IClusterFeaturesGrain GetClusterFeaturesGrain(this IOrleans orleans, Guid deployId)
    {
        throw new NotSupportedException("ClusterFeatures no longer uses a grain.");
    }
}

[Obsolete("ClusterFeatures no longer uses a grain. State is persisted directly via StateStorage.")]
public interface IClusterFeaturesGrain : IGrainWithGuidKey
{
    Task<ClusterFeaturesState> Get();
    Task<ClusterFeaturesState> Set(ClusterFeaturesState state);
}

[Obsolete("ClusterFeatures no longer uses a grain. State is persisted directly via StateStorage.")]
public class ClusterFeaturesGrain : Grain, IClusterFeaturesGrain
{
    public ClusterFeaturesGrain([State] State<ClusterFeaturesState> state)
    {
        _state = state;
    }

    private readonly State<ClusterFeaturesState> _state;

    public Task<ClusterFeaturesState> Get()
    {
        return _state.ReadValue();
    }

    public async Task<ClusterFeaturesState> Set(ClusterFeaturesState state)
    {
        return await _state.Update(s => {
            s.AcceptingConnections = state.AcceptingConnections;
            s.MatchmakingEnabled = state.MatchmakingEnabled;
            s.SideEffectsEnabled = state.SideEffectsEnabled;
            s.SnapshotDiffGuardEnabled = state.SnapshotDiffGuardEnabled;
        });
    }
}
