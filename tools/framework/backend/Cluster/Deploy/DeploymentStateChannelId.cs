using Infrastructure;

namespace Cluster.Deploy;

public class DeploymentStateChannelId<T> : IRuntimeChannelId
{
    public DeploymentStateChannelId(Guid deployId)
    {
        _deployId = deployId;
    }

    private readonly Guid _deployId;

    public string ToRaw()
    {
        var type = typeof(T);
        return $"deployment-state-{type.FullName}-{_deployId:N}";
    }
}
