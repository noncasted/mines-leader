using Common.Extensions;
using Infrastructure;
using Infrastructure.Execution;
using Microsoft.Extensions.Hosting;

namespace Cluster.Configs;

public static class ConfigsExtensions
{
    public static IHostApplicationBuilder AddConfigs(this IHostApplicationBuilder builder)
    {
        builder.AddAddressableState<CardConfigsState>()
            .As<ICardConfigs>();

        builder.AddAddressableState<BotConfigState>()
            .As<IBotConfig>();

        builder.AddAddressableState<GameModeConfigState>()
            .As<IGameModeConfig>();

        builder.AddAddressableState<RatingConfigState>()
            .As<IRatingConfig>();

        builder.AddAddressableState<SideEffectsConfigState>()
            .As<ISideEffectsConfig>();

        builder.AddAddressableState<MessageQueueConfigState>()
            .As<IMessageQueueConfig>();

        builder.AddAddressableState<TaskBalancerConfigState>()
            .As<ITaskBalancerConfig>();

        return builder;
    }
}