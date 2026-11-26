using Cluster.Discovery;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Cluster.Coordination;

public class ClusterStartupOptions
{
    // Set by the composition root through ConfigureRequiredServices; there is no default set.
    public IReadOnlyList<ServiceTag> RequiredServices { get; init; } = [];
}
