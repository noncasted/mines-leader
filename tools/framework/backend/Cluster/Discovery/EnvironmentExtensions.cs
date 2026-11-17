using Common.Extensions;
using Microsoft.Extensions.Hosting;

namespace Cluster.Discovery;

public static class EnvironmentExtensions
{
    public static IHostApplicationBuilder AddEnvironment(this IHostApplicationBuilder builder, ServiceTag tag)
    {
        builder.Add<IServiceEnvironment>(new ServiceEnvironment
        {
            Tag = tag
        });

        return builder;
    }
}
