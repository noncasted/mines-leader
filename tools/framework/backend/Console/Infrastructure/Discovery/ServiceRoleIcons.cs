using Cluster.Discovery;

namespace Console;

// Lucide icons of the service roles. The cluster role is built in, the project adds its own roles
// (Services.Configure<ServiceRoleIcons> in the console host).
public class ServiceRoleIcons
{
    public Dictionary<ServiceTag, string> Icons { get; } = new()
    {
        [ServiceTag.Coordinator] = "crown",
    };
}
