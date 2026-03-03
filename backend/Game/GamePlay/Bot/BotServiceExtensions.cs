using Microsoft.Extensions.DependencyInjection;

namespace Game.GamePlay;

public static class BotServiceExtensions
{
    public static IServiceCollection AddBotServices(this IServiceCollection services)
    {
        services.AddSingleton<IBotRunner, BotRunner>();
        services.AddSingleton<IBotContext, BotContext>();
        services.AddSingleton<IBotCardStrategies, BotCardStrategies>();

        services.AddSingleton<BotBoardUtils>();
        services.AddSingleton<IBotCommandUtils, BotCommandUtils>();

        services.AddSingleton<IBotCardAction, BotCardAction>();
        services.AddSingleton<IBotCellAction, BotCellAction>();
        services.AddSingleton<IBotFlagAction, BotFlagAction>();

        services.AddSingleton<IBotCardStrategy, BloodhoundStrategy>();
        services.AddSingleton<IBotCardStrategy, TrebuchetStrategy>();
        services.AddSingleton<IBotCardStrategy, TrebuchetAimerStrategy>();
        services.AddSingleton<IBotCardStrategy, ErosionDozerStrategy>();
        services.AddSingleton<IBotCardStrategy, GravediggerStrategy>();
        services.AddSingleton<IBotCardStrategy, ZipZapStrategy>();
        services.AddSingleton<IBotCardStrategy, OpponentFlagEraseStrategy>();
        services.AddSingleton<IBotCardStrategy, OpponentFlagReshuffleStrategy>();
        services.AddSingleton<IBotCardStrategy, OpponentBombStrategy>();
        services.AddSingleton<IBotCardStrategy, SmokeStrategy>();

        return services;
    }
}