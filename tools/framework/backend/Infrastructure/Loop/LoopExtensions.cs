using Common.Extensions;
using Internal;
using Microsoft.Extensions.Hosting;

namespace Infrastructure;

public static class LoopExtensions
{
    public static IHostApplicationBuilder AddServiceLoop(this IHostApplicationBuilder builder)
    {
        builder.Add<ServiceLoopObserver>()
               .As<IServiceLoopObserver>()
               .As<ILifecycleParticipant<IClusterClientLifecycle>>()
               .As<ILifecycleParticipant<ISiloLifecycle>>();

        builder.Add<ServiceLoop>()
               .As<IServiceLoop>();

        return builder;
    }
}
