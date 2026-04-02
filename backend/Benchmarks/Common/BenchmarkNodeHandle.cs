using System;
using Cluster.Discovery;
using Common.Extensions;
using Common.Reactive;

namespace Benchmarks;

public class BenchmarkNodeHandle
{
    public BenchmarkNodeHandle(
        ClusterTestUtils utils,
        IOperationProgress progress,
        IReadOnlyLifetime lifetime)
    {
        Progress = progress;
        Lifetime = lifetime;
        _utils = utils;
    }

    private readonly ClusterTestUtils _utils;
    private readonly List<(ServiceTag, string)> _startedNodes = new();
    private readonly BenchmarkMetricsHandle _metrics = new();

    public IOperationProgress Progress { get; }
    public IReadOnlyLifetime Lifetime { get; }
    public BenchmarkMetricsHandle Metrics => _metrics;


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
