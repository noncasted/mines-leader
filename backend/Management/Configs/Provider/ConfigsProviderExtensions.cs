using Common;
using Microsoft.Extensions.Hosting;
using ServiceLoop;

namespace Management.Configs;

public static class ConfigsProviderExtensions
{
    public static IHostApplicationBuilder AddConfigsServices(this IHostApplicationBuilder builder)
    {
        builder.Services.Add<ConfigsLoop>()
            .AsSetupLoopStage();

        builder.Services.Add<ConfigsObserver>()
            .AsSetupLoopStage();

        return builder;
    }
}