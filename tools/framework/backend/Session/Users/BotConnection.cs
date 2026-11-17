using Common.Network;
using Internal;
using Shared;

namespace Game.Session;

public class BotConnection : IConnection, IConnectionWriter, IConnectionReader
{
    private TaskCompletionSource? _closed;

    public required IReadOnlyLifetime Lifetime { get; init; }
    public IConnectionReader Reader => this;
    public IConnectionWriter Writer => this;

    public IViewableDelegate<OneWayMessageFromClient> OneWay { get; } = new ViewableDelegate<OneWayMessageFromClient>();

    public IViewableDelegate<RequestMessageFromClient> Requests { get; } =
        new ViewableDelegate<RequestMessageFromClient>();

    // A bot has no socket: it is closed when its lifetime ends.
    public Task Closed => (_closed ??= ListenEnd()).Task;

    public Task Run()
    {
        return Closed;
    }

    public void ForceDisconnect()
    {
    }

    public void WriteOneWay(INetworkContext context)
    {
    }

    public void WriteResponse(INetworkContext context, int requestId)
    {
    }

    private TaskCompletionSource ListenEnd()
    {
        var completion = new TaskCompletionSource();
        Lifetime.Listen(() => completion.TrySetResult());
        return completion;
    }
}
