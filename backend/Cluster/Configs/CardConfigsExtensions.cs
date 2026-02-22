using Common.Extensions;
using Infrastructure;
using Microsoft.Extensions.Hosting;

namespace Cluster.Configs;

public static class CardConfigsExtensions
{
    public static IHostApplicationBuilder AddCardConfigs(this IHostApplicationBuilder services)
    {
        services.Services.Add<CardConfigsView>()
            .As<ICardConfigs>()
            .As<ILocalSetupCompleted>();

        return services;
    }
}
