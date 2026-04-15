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
        services.AddSingleton<ICommand, PlayerLoadedCommand>();
        services.AddSingleton<ICommand, RematchRequestCommand>();

        services.AddSingleton<GameCommandUtils>();

        services.AddSingleton<IResponseCommand, CardAddCheat>();
        services.AddSingleton<IResponseCommand, CardDiscardCheat>();
        services.AddSingleton<IResponseCommand, ChangeManaCheat>();
        services.AddSingleton<IResponseCommand, ChangeMaxManaCheat>();
        services.AddSingleton<IResponseCommand, ChangeHealthCheat>();
        services.AddSingleton<IResponseCommand, ChangeMaxHealthCheat>();
        services.AddSingleton<IResponseCommand, ChangeMovesCheat>();
        services.AddSingleton<IResponseCommand, ChangeMaxMovesCheat>();
        services.AddSingleton<IResponseCommand, SetMaxManaCheat>();
        services.AddSingleton<IResponseCommand, SetMaxHealthCheat>();
        services.AddSingleton<IResponseCommand, SetMaxMovesCheat>();
        services.AddSingleton<IResponseCommand, RestoreManaCheat>();
        services.AddSingleton<IResponseCommand, RestoreHealthCheat>();
        services.AddSingleton<IResponseCommand, RestoreMovesCheat>();
        services.AddSingleton<IResponseCommand, EndMatchCheat>();

        return services;
    }
}