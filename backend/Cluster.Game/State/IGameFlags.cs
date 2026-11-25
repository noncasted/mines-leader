using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Cluster.State;

// Switches of this game, stored in the cluster features of the deploy (IClusterFeatures game flags).
public interface IGameFlags
{
    bool MatchmakingEnabled { get; }
    bool SnapshotDiffGuardEnabled { get; }

    // Cheat commands and the oracle of the agent observation; they also need a Development host (ICheatsAccess).
    bool CheatsEnabled { get; }
}

public static class GameFlagNames
{
    public const string Matchmaking = "Matchmaking";
    public const string SnapshotDiffGuard = "SnapshotDiffGuard";
    public const string Cheats = "Cheats";
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
    public bool CheatsEnabled => _features.IsGameFlagEnabled(GameFlagNames.Cheats);
}

public static class GameFlagsExtensions
{
    public static IHostApplicationBuilder AddGameFlags(this IHostApplicationBuilder builder)
    {
        builder.Services.AddSingleton<IGameFlags, GameFlags>();
        return builder;
    }
}
