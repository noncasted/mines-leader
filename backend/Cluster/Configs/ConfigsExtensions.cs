using Common.Extensions;
using Infrastructure;
using Microsoft.Extensions.Hosting;

namespace Cluster.Configs;

public static class ConfigsExtensions
{
    public static IHostApplicationBuilder AddConfigs(this IHostApplicationBuilder builder)
    {
        builder.Add<CardConfigsView>()
            .As<ICardConfigs>()
            .As<ILocalSetupCompleted>();

        builder.Add<BotConfigView>()
            .As<IBotConfig>()
            .As<ILocalSetupCompleted>();

        builder.Add<GameModeConfigView>()
            .As<IGameModeConfig>()
            .As<ILocalSetupCompleted>();
        
        return builder;
    }
}
