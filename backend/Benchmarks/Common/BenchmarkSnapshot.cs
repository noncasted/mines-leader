namespace Benchmarks;

public class BenchmarkSnapshot {
    public Guid ResultId { get; set; }
    public int StepIndex { get; set; }
    public float StepPercent { get; set; }
    public double MetricValue { get; set; }
    public double DiffValue { get; set; }
}
