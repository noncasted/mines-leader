using Infrastructure;

namespace Cluster.Deploy;

public static class DeployIdPipe
{
    public static readonly IRuntimePipeId Id = new RuntimePipeId("deploy-id");
}
