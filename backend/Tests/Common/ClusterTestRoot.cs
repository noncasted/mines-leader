using Cluster.Discovery;
using Common.Extensions;
using Common.Reactive;
using Infrastructure;
using Microsoft.Extensions.Logging;

namespace Tests;

public abstract class ClusterTestRoot<TPayload>
{
    public ClusterTestRoot(ClusterTestUtils utils)
    {
        _utils = utils;
    }

    private readonly ClusterTestUtils _utils;

    public IMessaging Messaging => _utils.Messaging;
    public IServiceEnvironment Environment => _utils.Environment;
    public ILogger Logger => _utils.Logger;
    public ClusterTestUtils Utils => _utils;

    protected abstract string Name { get; }

    public async Task Start(IOperationProgress progress, TPayload payload)
    {
        var lifetime = new Lifetime();
        var handle = new ClusterTestNodeHandle(_utils, progress, lifetime);
        progress.SetStatus(OperationStatus.Preparing);

        try
        {
            await Run(handle, payload);
            progress.SetStatus(OperationStatus.Success);
        }
        catch (Exception e)
        {
            progress.SetStatus(OperationStatus.Failed);
            Logger.LogError(e, "Test {TestName} failed with exception", Name);
        }
        
        lifetime.Terminate();
        await handle.TerminateAllNodes();
    }

    protected abstract Task Run(ClusterTestNodeHandle handle, TPayload payload);
}