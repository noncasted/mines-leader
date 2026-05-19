using System.Net.WebSockets;
using Common.Extensions;
using Common.Reactive;
using Microsoft.Extensions.Logging;

namespace Common.Network;

public interface IConnection
{
    IReadOnlyLifetime Lifetime { get; }
    IConnectionReader Reader { get; }
    IConnectionWriter Writer { get; }

    Task Run();
    void ForceDisconnect();
}

public class Connection : IConnection
{
    public Connection(WebSocket webSocket, IReadOnlyLifetime parentLifetime, ILogger logger)
    {
        _webSocket = webSocket;
        _lifetime = parentLifetime.Child();
        _reader = new ConnectionReader(webSocket, logger);
        _writer = new ConnectionWriter(webSocket, logger);
    }

    private readonly WebSocket _webSocket;
    private readonly ILifetime _lifetime;
    private readonly ConnectionReader _reader;
    private readonly ConnectionWriter _writer;

    public IReadOnlyLifetime Lifetime => _lifetime;
    public IConnectionReader Reader => _reader;
    public IConnectionWriter Writer => _writer;

    public async Task Run()
    {
        if (_lifetime.IsTerminated == true)
            throw new InvalidOperationException("Connection is terminated");

        _writer.Run(_lifetime).NoAwait();
        await _reader.Run(_lifetime);

        if (_lifetime.IsTerminated == false)
            _lifetime.Terminate();

        OnDisconnected();
    }

    public void ForceDisconnect()
    {
        _lifetime.Terminate();
        OnDisconnected();
    }

    private void OnDisconnected()
    {
        try
        {
            _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "User disconnected", CancellationToken.None)
                .NoAwait();
        }
        catch (Exception)
        {
            // CloseAsync is best-effort; ignore failures on already-closed sockets.
        }
    }
}
