using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shared;

namespace Game.GamePlay;

public static class PlayerServiceExtensions
{
    public static IServiceCollection AddPlayerServices(this IServiceCollection services)
    {
        // The session container has no AddOptions(): the boards take the default size explicitly.
        services.AddSingleton(Options.Create(new BoardOptions()));
        services.AddSingleton<IPlayerFactory, PlayerFactory>();

        return services;
    }
}
