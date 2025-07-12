using Microsoft.Extensions.DependencyInjection;

namespace Game.GamePlay;

public static class PlayerServiceExtensions
{
    public static IServiceCollection AddPlayerServices(this IServiceCollection services)
    {
        services.AddSingleton<IPlayerFactory, PlayerFactory>();

        return services;
    }
}