using Common;
using Common.Extensions;
using Cluster.Deploy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shared;

namespace Cluster.State;

public static class ClusterFeaturesExtensions
{
    public static IHostApplicationBuilder AddClusterFeatures(this IHostApplicationBuilder builder)
    {
        builder.Add<ClusterFeatures>()
               .As<IClusterFeatures>()
               .As<IDeployAware>();

        builder.Services.AddSingleton<IClusterFlags>(sp => sp.GetRequiredService<IClusterFeatures>());

        return builder;
    }
}
