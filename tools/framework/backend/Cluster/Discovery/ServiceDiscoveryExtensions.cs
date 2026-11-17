using Common.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Cluster.Discovery;

public static class ServiceDiscoveryExtensions
{
    public static IHostApplicationBuilder AddServiceDiscovery(this IHostApplicationBuilder builder)
    {
        builder.Add<ServiceDiscovery>()
               .As<IServiceDiscovery>();

        return builder;
    }

    // Marks the host as a game server (ServiceDiscoveryOptions.PublishGameServerUrl).
    public static IHostApplicationBuilder PublishGameServerUrl(this IHostApplicationBuilder builder)
    {
        builder.Services.AddSingleton(Options.Create(new ServiceDiscoveryOptions { PublishGameServerUrl = true }));
        return builder;
    }
}
