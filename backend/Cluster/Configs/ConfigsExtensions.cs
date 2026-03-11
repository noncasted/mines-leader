using Common.Extensions;
using Infrastructure;
using Microsoft.Extensions.Hosting;

namespace Cluster.Configs;

public static class ConfigsExtensions
{
    public static IHostApplicationBuilder AddConfigs(this IHostApplicationBuilder builder)
    {
        var services = builder.Services;
        
        services.Add<CardConfigsView>()
            .As<ICardConfigs>()
            .As<ILocalSetupCompleted>();

        services.Add<BotConfigView>()
            .As<IBotConfig>()
            .As<ILocalSetupCompleted>();

        services.Add<GameModeConfigView>()
            .As<IGameModeConfig>()
            .As<ILocalSetupCompleted>();
        
        return builder;
    }
}
