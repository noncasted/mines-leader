namespace Cluster.Deploy;

public class CoordinatorHealthOptions
{
    // Heartbeat older than this marks the coordinator unhealthy (readiness check, DeployHealthChecker).
    public TimeSpan StaleThreshold { get; init; } = TimeSpan.FromSeconds(15);

    // How often every service's DeployHealthChecker polls the deploy id pipe and the heartbeat.
    public TimeSpan HealthPollInterval { get; init; } = TimeSpan.FromSeconds(10);

    // Coordinator side: how often DeployIdentity writes the heartbeat and how many failed writes
    // in a row stop the coordinator process.
    public TimeSpan HeartbeatInterval { get; init; } = TimeSpan.FromSeconds(5);
    public int MaxConsecutiveHeartbeatFailures { get; init; } = 3;
}
