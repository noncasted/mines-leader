using Internal;

namespace Cluster.Deploy;

public interface IDeployAware
{
    Task OnDeployChanged(Guid newDeployId, IReadOnlyLifetime deployLifetime);
}
