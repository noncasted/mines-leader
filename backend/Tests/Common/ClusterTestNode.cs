using Cluster.Discovery;
using Common.Extensions;
using Common.Reactive;
using Infrastructure;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Tests;

public abstract class ClusterTestNode<TPayload> : ICoordinatorSetupCompleted
{
    public ClusterTestNode(ClusterTestUtils utils)
    {
        _utils = utils;
    }

    private readonly ClusterTestUtils _utils;

    public IMessaging Messaging => _utils.Messaging;
    public IServiceEnvironment Environment => _utils.Environment;
    public ILogger Logger => _utils.Logger;

    private ILifetime _testLifetime;

    protected abstract string Name { get; }

    public async Task OnCoordinatorSetupCompleted(IReadOnlyLifetime lifetime)
    {
        await Messaging
            .AddPipeRequestHandler<ClusterTestNodeMessages.StartRequest, ClusterTestNodeMessages.StartResponse>(
                lifetime,
                new ClusterTestNodeMessages.PipeId(Environment.Tag, Name, "start"),
                OnStartRequest
            );

        await Messaging.ListenPipe<ClusterTestNodeMessages.Terminate>(
            lifetime,
            new ClusterTestNodeMessages.PipeId(Environment.Tag, Name, "terminate"),
            OnTerminate);
    }

    private async Task<ClusterTestNodeMessages.StartResponse> OnStartRequest(
        ClusterTestNodeMessages.StartRequest request)
    {
        var payload = default(TPayload);

        if (request.Payload is not null)
            payload = (TPayload)request.Payload;

        _testLifetime = new Lifetime();
        Run(_testLifetime, payload).NoAwait();
        return new ClusterTestNodeMessages.StartResponse();
    }

    private void OnTerminate(ClusterTestNodeMessages.Terminate message)
    {
        _testLifetime.Terminate();
    }

    protected abstract Task Run(IReadOnlyLifetime lifetime, TPayload payload);
}

public static class ClusterTestNodeExtensions
{
    public static IHostApplicationBuilder AddClusterTestNode<TNode>(this IHostApplicationBuilder builder)
        where TNode : class
    {
        builder.Add<TNode>()
            .As<ICoordinatorSetupCompleted>();

        return builder;
    }
}