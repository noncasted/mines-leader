using Cluster.Discovery;

namespace Cluster.Coordination;

public interface IClusterStartupConfig
{
    IReadOnlyList<ServiceTag> RequiredServices { get; }
}

public class ClusterStartupConfig : IClusterStartupConfig
{
    public ClusterStartupConfig()
    {
        var required = new List<ServiceTag>
        {
            ServiceTag.Coordinator,
            ServiceTag.Meta,
            ServiceTag.Game,
            ServiceTag.Silo,
            ServiceTag.Console
        };

        RequiredServices = required;
    }

    public IReadOnlyList<ServiceTag> RequiredServices { get; }
}
