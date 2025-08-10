using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Game.GamePlay;

public static class GameCommandsExtensions
{
    public static IServiceCollection AddGameCommands(this IServiceCollection services)
    {
        services.AddSingleton<IResponseCommand, BoardCardUse>();
        services.AddSingleton<IResponseCommand, OpenCellAction>();
        services.AddSingleton<ICommand, PlayerReadyCommand>();
        services.AddSingleton<IResponseCommand, RemoveFlagAction>();
        services.AddSingleton<IResponseCommand, SetFlagAction>();
        services.AddSingleton<GameCommandUtils>();
        
        return services;
    }
}