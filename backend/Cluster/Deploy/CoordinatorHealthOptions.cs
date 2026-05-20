namespace Cluster.Deploy;

public class CoordinatorHealthOptions
{
    public TimeSpan StaleThreshold { get; set; } = TimeSpan.FromSeconds(15);
}
