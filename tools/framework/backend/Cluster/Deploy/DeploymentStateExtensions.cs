using Common.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Cluster.Deploy;

public static class DeploymentStateExtensions
{
    public static IHostApplicationBuilder AddDeploymentState<T>(this IHostApplicationBuilder builder)
        where T : class, new()
    {
        builder.Services.AddSingleton(new T());

        builder.Add<DeploymentState<T>>()
               .As<IDeploymentState<T>>()
               .As<IDeployAware>();

        return builder;
    }
}
