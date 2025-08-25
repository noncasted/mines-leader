using Microsoft.Extensions.DependencyInjection;

namespace Game.GamePlay;

public static class CardServiceExtensions
{
    public static IServiceCollection AddCardServices(this IServiceCollection services)
    {
        services.AddSingleton<ICardFactory, CardFactory>();

        return services;
    }
}