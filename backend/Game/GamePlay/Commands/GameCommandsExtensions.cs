using Microsoft.Extensions.DependencyInjection;

namespace Game.GamePlay;

public static class GameCommandsExtensions
{
    public static IServiceCollection AddGameCommands(this IServiceCollection services)
    {
        services.AddSingleton<IResponseCommand, CardUse>();
        services.AddSingleton<IResponseCommand, OpenCellAction>();
        services.AddSingleton<ICommand, PlayerReadyCommand>();
        services.AddSingleton<IResponseCommand, RemoveFlagAction>();
        services.AddSingleton<IResponseCommand, SetFlagAction>();
        services.AddSingleton<IResponseCommand, SkipTurn>();
        services.AddSingleton<GameCommandUtils>();
        
        return services;
    }
}