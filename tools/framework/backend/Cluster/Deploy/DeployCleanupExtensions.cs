using Common.Extensions;
using Microsoft.Extensions.Hosting;

namespace Cluster.Deploy;

public static class DeployCleanupExtensions
{
    public static IHostApplicationBuilder AddDeployCleanup(this IHostApplicationBuilder builder)
    {
        builder.Add<DeployCleanup>()
               .As<IDeployCleanup>();

        return builder;
    }
}
