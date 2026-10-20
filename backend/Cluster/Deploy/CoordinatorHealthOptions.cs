namespace Cluster.Deploy;

public class CoordinatorHealthOptions
{
    // Heartbeat older than this marks the coordinator unhealthy (readiness check).
    public TimeSpan StaleThreshold { get; set; } = TimeSpan.FromSeconds(15);

    // Coordinator side: how often DeployIdentity writes the heartbeat and how many failed writes
    // in a row stop the coordinator process.
    public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.FromSeconds(5);
    public int MaxConsecutiveHeartbeatFailures { get; set; } = 3;
}
