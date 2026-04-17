using Common;
using Common.Extensions;
using Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Cluster.State;

public interface IClusterFeatures : IAddressableState<ClusterFeaturesState>, IClusterFlags
{
    Task SetAcceptingConnections(bool accepting);
    Task SetMatchmakingEnabled(bool enabled);
    Task SetSideEffectsEnabled(bool enabled);
    Task SetSnapshotDiffGuardEnabled(bool enabled);
}

public class ClusterFeatures(AddressableStateUtils utils)
    : AddressableState<ClusterFeaturesState>(utils), IClusterFeatures
{
    bool IClusterFlags.MatchmakingEnabled => Value.MatchmakingEnabled;
    bool IClusterFlags.SideEffectsEnabled => Value.SideEffectsEnabled;
    bool IClusterFlags.SnapshotDiffGuardEnabled => Value.SnapshotDiffGuardEnabled;

    public Task SetAcceptingConnections(bool accepting)
    {
        var current = Value;

        return SetValue(new ClusterFeaturesState
        {
            AcceptingConnections = accepting,
            MatchmakingEnabled = current.MatchmakingEnabled,
            SideEffectsEnabled = current.SideEffectsEnabled,
            SnapshotDiffGuardEnabled = current.SnapshotDiffGuardEnabled
        });
    }

    public Task SetMatchmakingEnabled(bool enabled)
    {
        var current = Value;

        return SetValue(new ClusterFeaturesState
        {
            AcceptingConnections = current.AcceptingConnections,
            MatchmakingEnabled = enabled,
            SideEffectsEnabled = current.SideEffectsEnabled,
            SnapshotDiffGuardEnabled = current.SnapshotDiffGuardEnabled
        });
    }

    public Task SetSideEffectsEnabled(bool enabled)
    {
        var current = Value;

        return SetValue(new ClusterFeaturesState
        {
            AcceptingConnections = current.AcceptingConnections,
            MatchmakingEnabled = current.MatchmakingEnabled,
            SideEffectsEnabled = enabled,
            SnapshotDiffGuardEnabled = current.SnapshotDiffGuardEnabled
        });
    }

    public Task SetSnapshotDiffGuardEnabled(bool enabled)
    {
        var current = Value;

        return SetValue(new ClusterFeaturesState
        {
            AcceptingConnections = current.AcceptingConnections,
            MatchmakingEnabled = current.MatchmakingEnabled,
            SideEffectsEnabled = current.SideEffectsEnabled,
            SnapshotDiffGuardEnabled = enabled
        });
    }
}

[GenerateSerializer]
[GrainState(Table = "configs", State = "cluster_features", Lookup = "ClusterFeatures", Key = GrainKeyType.String)]
public class ClusterFeaturesState
{
    [Id(0)]
    public bool AcceptingConnections { get; set; } = false;

    [Id(1)]
    public bool MatchmakingEnabled { get; set; } = false;

    [Id(2)]
    public bool SideEffectsEnabled { get; set; } = false;

    [Id(3)]
    public bool SnapshotDiffGuardEnabled { get; set; } = false;
}

public static class ClusterFeaturesExtensions
{
    public static IHostApplicationBuilder AddClusterFeatures(this IHostApplicationBuilder builder)
    {
        builder.AddAddressableState<ClusterFeatures>()
               .As<IClusterFeatures>();

        builder.Services.AddSingleton<IClusterFlags>(sp => sp.GetRequiredService<IClusterFeatures>());

        return builder;
    }
}