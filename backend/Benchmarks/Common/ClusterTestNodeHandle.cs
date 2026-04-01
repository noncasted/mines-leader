using System;
using Cluster.Discovery;
using Common.Extensions;
using Common.Reactive;

namespace Benchmarks;

public class ClusterTestNodeHandle
{
    public ClusterTestNodeHandle(
        ClusterTestUtils utils,
        IOperationProgress progress,
        IReadOnlyLifetime lifetime,
        Action<double>? reportMetric = null)
    {
        Progress = progress;
        Lifetime = lifetime;
        _utils = utils;
        _reportMetric = reportMetric;
    }

    private readonly ClusterTestUtils _utils;
    private readonly Action<double>? _reportMetric;
    private readonly List<(ServiceTag, string)> _startedNodes = new();
    private readonly List<BenchmarkSnapshot> _snapshots = new();
    private double _previousCumulativeValue;

    public IOperationProgress Progress { get; }
    public IReadOnlyLifetime Lifetime { get; }
    public IReadOnlyList<BenchmarkSnapshot> Snapshots => _snapshots;

    public void ReportMetric(double value)
    {
        _reportMetric?.Invoke(value);
    }

    public void RecordSnapshot(float stepPercent, double cumulativeValue)
    {
        var diff = cumulativeValue - _previousCumulativeValue;
        _snapshots.Add(new BenchmarkSnapshot {
            StepIndex = _snapshots.Count,
            StepPercent = stepPercent,
            MetricValue = cumulativeValue,
            DiffValue = diff
        });
        _previousCumulativeValue = cumulativeValue;
    }

    public Task StartNode(ServiceTag service, string nodeName, object? payload = null)
    {
        _startedNodes.Add((service, nodeName));
        return _utils.StartNode(service, nodeName, payload);
    }

    public async Task TerminateAllNodes()
    {
        foreach (var (service, nodeName) in _startedNodes)
            await _utils.TerminateNode(service, nodeName);
    }
}
