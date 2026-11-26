namespace Cluster.Monitoring;

// A host that writes the dev metrics snapshots (AddMetricsSnapshots), kept in its container: the monolith runner writes
// one snapshot for the process when any of its hosts has it.
public record MetricsSnapshotRegistration;
