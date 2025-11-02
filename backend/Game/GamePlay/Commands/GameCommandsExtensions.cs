using Game.Session;
using Microsoft.Extensions.DependencyInjection;

namespace Game.GamePlay;

public static class GameCommandsExtensions
{
    public static IServiceCollection AddGameCommands(this IServiceCollection services)
    {
        services.AddSingleton<IResponseCommand, CardUseCommand>();
        services.AddSingleton<IResponseCommand, OpenCellCommand>();
        services.AddSingleton<IResponseCommand, OpenMultipleCellsCommand>();
        services.AddSingleton<IResponseCommand, RemoveFlagAction>();
        services.AddSingleton<IResponseCommand, SetFlagAction>();
        services.AddSingleton<IResponseCommand, SkipTurn>();

        services.AddSingleton<ICommand, PlayerReadyCommand>();
        services.AddSingleton<ICommand, RematchRequestCommand>();

        services.AddSingleton<GameCommandUtils>();

        services.AddSingleton<IResponseCommand, CardAddCheat>();
        services.AddSingleton<IResponseCommand, CardDiscardCheat>();
        services.AddSingleton<IResponseCommand, ChangeManaCheat>();
        services.AddSingleton<IResponseCommand, ChangeHealthCheat>();
        services.AddSingleton<IResponseCommand, ChangeMovesCheat>();
        services.AddSingleton<IResponseCommand, EndMatchCheat>();

        return services;
    }
}