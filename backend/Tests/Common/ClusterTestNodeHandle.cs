using Cluster.Discovery;
using Common.Extensions;
using Common.Reactive;

namespace Tests;

public class ClusterTestNodeHandle
{
    public ClusterTestNodeHandle(
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

    public IOperationProgress Progress { get; }
    public IReadOnlyLifetime Lifetime { get; }

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