using Common;
using Microsoft.Extensions.DependencyInjection;

namespace Game.GamePlay;

public static class GameContextServiceExtensions
{
    public static IServiceCollection AddGameContext(this IServiceCollection services)
    {
        services.Add<IGameContext, GameContext>();

        services.Add<GameRound>()
            .As<IService>()
            .As<IUsersConnected>()
            .As<IGameRound>();

        services.AddSingleton<ISnapshotSender, SnapshotSender>();
        services.AddSingleton<IGameReadyAwaiter, GameReadyAwaiter>();

        return services;
    }
}