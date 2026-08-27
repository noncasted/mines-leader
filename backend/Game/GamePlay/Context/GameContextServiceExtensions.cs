using Common.Extensions;
using Game.GamePlay.Snapshots;
using Game.Session;
using Microsoft.Extensions.DependencyInjection;

namespace Game.GamePlay;

public static class GameContextServiceExtensions
{
    public static IServiceCollection AddGameContext(this IServiceCollection services)
    {
        services.Add<IGameContext, GameContext>();

        services.Add<MatchStatsTracker>()
                .As<IMatchStatsTracker>();

        services.Add<GameRandom>()
                .As<IGameRandom>();

        services.Add<RoundActionService>()
                .As<IRoundActionService>();

        services.Add<RematchAwaiter>()
                .As<IRematchAwaiter>();

        services.Add<GameFlow>()
                .As<IService>()
                .As<IGameFlow>();

        services.Add<SnapshotSender>()
                .As<ISnapshotSender>();

        services.Add<SnapshotDiffGuard>()
                .As<ISnapshotDiffGuard>();

        services.Add<AgentObservationPublisher>()
                .As<IAgentObservationPublisher>();

        return services;
    }
}