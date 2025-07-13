﻿using System.Net.WebSockets;
using Microsoft.Extensions.Logging;

namespace Common;

public interface IConnection
{
    IReadOnlyLifetime Lifetime { get; }
    IConnectionReader Reader { get; }
    IConnectionWriter Writer { get; }
    IConnectionPing Ping { get; }

    void OnPingFailed();
}

public class Connection : IConnection
{
    public Connection(WebSocket webSocket, IReadOnlyLifetime parentLifetime, ILogger logger)
    {
        _webSocket = webSocket;
        _lifetime = parentLifetime.Child();
        _reader = new ConnectionReader(webSocket);
        _writer = new ConnectionWriter(webSocket, logger);
        _ping = new ConnectionPing(_writer);
    }

    private readonly WebSocket _webSocket;
    private readonly ILifetime _lifetime;
    private readonly ConnectionReader _reader;
    private readonly ConnectionWriter _writer;
    private readonly ConnectionPing _ping;

    public IReadOnlyLifetime Lifetime => _lifetime;
    public IConnectionReader Reader => _reader;
    public IConnectionWriter Writer => _writer;
    public IConnectionPing Ping => _ping;

    public async Task Run()
    {
        _writer.Run(_lifetime).NoAwait();
        await _reader.Run(_lifetime);
        
        if (_lifetime.IsTerminated == true)
            return;
        
        _lifetime.Terminate();
        OnDisconnected();
    }
    
    public void OnPingFailed()
    {
        _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Ping failed", CancellationToken.None);
    }

    private void OnDisconnected()
    {
        _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "User disconnected", CancellationToken.None);
    }
}