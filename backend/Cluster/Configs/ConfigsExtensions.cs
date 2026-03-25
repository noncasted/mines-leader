using Common.Extensions;
using Infrastructure;
using Microsoft.Extensions.Hosting;

namespace Cluster.Configs;

public static class ConfigsExtensions
{
    public static IHostApplicationBuilder AddConfigs(this IHostApplicationBuilder builder)
    {
        builder.Add<CardConfigsState>()
            .As<ICardConfigs>()
            .As<ILocalSetupCompleted>();

        builder.Add<BotConfigState>()
            .As<IBotConfig>()
            .As<ILocalSetupCompleted>();

        builder.Add<GameModeConfigState>()
            .As<IGameModeConfig>()
            .As<ILocalSetupCompleted>();

        return builder;
    }
}