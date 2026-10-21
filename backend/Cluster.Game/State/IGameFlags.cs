using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Cluster.State;

// Switches of this game, stored in the cluster features of the deploy (IClusterFeatures game flags).
public interface IGameFlags
{
    bool MatchmakingEnabled { get; }
    bool SnapshotDiffGuardEnabled { get; }
}

public static class GameFlagNames
{
    public const string Matchmaking = "Matchmaking";
    public const string SnapshotDiffGuard = "SnapshotDiffGuard";
}

public class GameFlags : IGameFlags
{
    public GameFlags(IClusterFeatures features)
    {
        _features = features;
    }

    private readonly IClusterFeatures _features;

    public bool MatchmakingEnabled => _features.IsGameFlagEnabled(GameFlagNames.Matchmaking);
    public bool SnapshotDiffGuardEnabled => _features.IsGameFlagEnabled(GameFlagNames.SnapshotDiffGuard);
}

public static class GameFlagsExtensions
{
    public static IHostApplicationBuilder AddGameFlags(this IHostApplicationBuilder builder)
    {
        builder.Services.AddSingleton<IGameFlags, GameFlags>();
        return builder;
    }
}
