using Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Orleans.Configuration.Internal;

namespace ServiceLoop;

public interface IOrleansLoopStage
{
    Task OnOrleansStage(IReadOnlyLifetime lifetime);
}

public interface IMessagingLoopStage
{
    Task OnMessagingStage(IReadOnlyLifetime lifetime);
}

public interface ISetupLoopStage
{
    Task OnSetupStage(IReadOnlyLifetime lifetime);
}

public static class LoopExtensions
{
    public static IHostApplicationBuilder AddServiceLoop(this IHostApplicationBuilder builder)
    {
        builder.Services.Add<ServiceLoopObserver>()
            .As<IServiceLoopObserver>()
            .As<ILifecycleParticipant<IClusterClientLifecycle>>();
        
        builder.Services.AddHostedService<ServiceLoop>();
        return builder;
    }

    public static ContainerExtensions.Registration AsOrleansLoopStage(
        this ContainerExtensions.Registration registration)
    {
        return registration
            .As<IOrleansLoopStage>();
    }

    public static ContainerExtensions.Registration AsMessagingLoopStage(
        this ContainerExtensions.Registration registration)
    {
        return registration
            .As<IMessagingLoopStage>();
    }

    public static ContainerExtensions.Registration AsSetupLoopStage(
        this ContainerExtensions.Registration registration)
    {
        return registration
            .As<ISetupLoopStage>();
    }
}