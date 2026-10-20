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

    public IViewableDelegate<RequestMessageFromClient> Requests { get; } =
        new ViewableDelegate<RequestMessageFromClient>();

    public IViewableDelegate<ResponseMessageFromClient> Responses { get; } =
        new ViewableDelegate<ResponseMessageFromClient>();

    public Task Run()
    {
        var completion = new TaskCompletionSource();
        Lifetime.Listen(() => completion.SetResult());
        return completion.Task;
    }

    public void ForceDisconnect()
    {

    }

    public void WriteOneWay(INetworkContext context)
    {
    }

    // A bot has no client: nothing would ever answer the request.
    public Task<T> WriteRequest<T>(INetworkContext context) where T : INetworkContext
    {
        return Task.FromException<T>(new InvalidOperationException(
            $"Request {ErrorResponse.Describe(context)} to a bot: a bot has no client to answer it"));
    }

    public void WriteResponse(INetworkContext context, int requestId)
    {
    }

    public void OnRequestHandled(INetworkContext context, int requestId)
    {
    }
}