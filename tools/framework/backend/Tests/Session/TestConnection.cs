using Common.Network;
using Internal;
using Game.Session;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Shared;

namespace Framework.Tests.Session;

// The server side of a client connection: records what the session sends, delivers what the test makes the client
// send. Like Common.Network.Connection: its lifetime is a child of the session one, Run completes when the connection
// is closed (by the client, forced by the session, or with the session), also on a connection terminated before it.
public class TestConnection : IConnection, IConnectionReader, IConnectionWriter
{
    public TestConnection(IReadOnlyLifetime parentLifetime)
    {
        _lifetime = parentLifetime.Child();
        _lifetime.Listen(() => _run.TrySetResult());
    }

    private readonly TaskCompletionSource _run = new();
    private readonly ILifetime _lifetime;
    private readonly List<INetworkContext> _sent = new();
    private readonly ViewableDelegate<OneWayMessageFromClient> _oneWay = new();
    private readonly ViewableDelegate<RequestMessageFromClient> _requests = new();

    public IReadOnlyLifetime Lifetime => _lifetime;
    public IConnectionReader Reader => this;
    public IConnectionWriter Writer => this;
    public IViewableDelegate<OneWayMessageFromClient> OneWay => _oneWay;
    public IViewableDelegate<RequestMessageFromClient> Requests => _requests;
    public Task Closed => _run.Task;

    public int ForceDisconnects { get; private set; }

    public IReadOnlyList<INetworkContext> Sent
    {
        get
        {
            lock (_sent)
                return _sent.ToList();
        }
    }

    // Not null: Run fails with it (a transport error of the socket).
    public Exception? RunFailure { get; init; }

    public Task Run()
    {
        if (RunFailure != null)
            return Task.FromException(RunFailure);

        return _run.Task;
    }

    public void ForceDisconnect()
    {
        ForceDisconnects++;
        Close();
    }

    public void Close()
    {
        _lifetime.Terminate();
    }

    public void ReceiveOneWay(INetworkContext context)
    {
        _oneWay.Invoke(new OneWayMessageFromClient
        {
            Context = context
        });
    }

    public void ClearSent()
    {
        lock (_sent)
            _sent.Clear();
    }

    public void WriteOneWay(INetworkContext context)
    {
        lock (_sent)
            _sent.Add(context);
    }

    public void WriteResponse(INetworkContext context, int requestId)
    {
        lock (_sent)
            _sent.Add(context);
    }
}
