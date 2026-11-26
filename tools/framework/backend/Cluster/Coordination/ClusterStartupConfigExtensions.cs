using Cluster.Discovery;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Cluster.Coordination;

public static class ClusterStartupConfigExtensions
{
    public static IHostApplicationBuilder ConfigureRequiredServices(
        this IHostApplicationBuilder builder,
        params ServiceTag[] required)
    {
        builder.Services.AddSingleton(Options.Create(new ClusterStartupOptions { RequiredServices = required }));
        return builder;
    }
}
