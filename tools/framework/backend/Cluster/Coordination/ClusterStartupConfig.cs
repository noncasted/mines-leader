using Cluster.Discovery;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Cluster.Coordination;

public interface IClusterStartupConfig
{
    // Roles that must be present in the service discovery before a participant finishes its startup,
    // in the order the console lists them.
    IReadOnlyList<ServiceTag> RequiredServices { get; }
}

public class ClusterStartupConfig : IClusterStartupConfig
{
    public ClusterStartupConfig(IOptions<ClusterStartupOptions> options)
    {
        var required = options.Value.RequiredServices;

        if (required.Count == 0)
            throw new InvalidOperationException(
                "Cluster startup requires at least one service role: call ConfigureRequiredServices in the composition root");

        if (required.Distinct().Count() != required.Count)
            throw new InvalidOperationException(
                $"Cluster startup required services contain duplicates: {string.Join(", ", required)}");

        RequiredServices = required.ToArray();
    }

    public IReadOnlyList<ServiceTag> RequiredServices { get; }
}
