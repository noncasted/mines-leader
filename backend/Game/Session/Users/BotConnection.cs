using Common.Network;
using Common.Reactive;
using Shared;

namespace Game.Session;

public class BotConnection : IConnection, IConnectionWriter, IConnectionReader
{
    public required IReadOnlyLifetime Lifetime { get; init; }
    public IConnectionReader Reader => this;
    public IConnectionWriter Writer => this;

    public IViewableDelegate<OneWayMessageFromClient> OneWay { get; } = new ViewableDelegate<OneWayMessageFromClient>();
    public IViewableDelegate<RequestMessageFromClient> Requests { get; } = new ViewableDelegate<RequestMessageFromClient>();
    public IViewableDelegate<ResponseMessageFromClient> Responses { get; } = new ViewableDelegate<ResponseMessageFromClient>();
    
    public Task Run()
    {
        var completion = new TaskCompletionSource();
        Lifetime.Listen(() => completion.SetResult());
        return completion.Task;
    }

    public void ForceDisconnect()
    {
        
    }

    public ValueTask WriteOneWay(INetworkContext context)
    {
        return ValueTask.CompletedTask;
    }

    public Task<INetworkContext?> WriteRequest<T>(INetworkContext context) where T : INetworkContext
    {
        return Task.FromResult<INetworkContext?>(null);
    }

    public ValueTask WriteResponse(INetworkContext context, int requestId)
    {
        return ValueTask.CompletedTask;
    }

    public void OnRequestHandled(INetworkContext context, int requestId)
    {
    }
}