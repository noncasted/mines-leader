using Common.Extensions;
using Game.Session;
using Microsoft.Extensions.DependencyInjection;

namespace Game.GamePlay;

public static class GameContextServiceExtensions
{
    public static IServiceCollection AddGameContext(this IServiceCollection services)
    {
        services.Add<IGameContext, GameContext>();

        services.Add<RoundActionService>()
            .As<IRoundActionService>();

        services.Add<RematchAwaiter>()
            .As<IRematchAwaiter>();

        services.Add<GameFlow>()
            .As<IService>()
            .As<IGameFlow>();

        services.Add<SnapshotSender>()
            .As<ISnapshotSender>();

        return services;
    }
}