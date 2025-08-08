using Common;
using Context;
using Microsoft.Extensions.DependencyInjection;

namespace Game.GamePlay;

public static class GameContextExtensions
{
    public static IServiceCollection AddGameContext(this IServiceCollection services)
    {
        services.Add<IGameContext, GameContext>();

        services.Add<GameRound>()
            .As<IService>();

        return services;
    }
}