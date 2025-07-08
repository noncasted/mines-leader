using Common;
using Microsoft.Extensions.Hosting;
using ServiceLoop;

namespace Management.Configs;

public static class ConfigsProviderExtensions
{
    public static IHostApplicationBuilder AddConfigsServices(this IHostApplicationBuilder builder)
    {
        builder.AddSingleton<ConfigsLoop>()
            .AsSetupLoopStage();

        builder.AddSingleton<ConfigsObserver>()
            .AsSetupLoopStage();

        return builder;
    }
}