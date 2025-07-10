using System.Net.WebSockets;
using MemoryPack;
using Shared;

namespace Common;

public interface IConnectionReader
{
    IViewableDelegate<IServerRequest> Received { get; }
    
    Task Run(IReadOnlyLifetime lifetime);
}

public class ConnectionReader : IConnectionReader
{
    public ConnectionReader(WebSocket webSocket)
    {
        _webSocket = webSocket;
    }

    private readonly WebSocket _webSocket;
    private readonly ViewableDelegate<IServerRequest> _received = new();

    public IViewableDelegate<IServerRequest> Received => _received;

    public async Task Run(IReadOnlyLifetime lifetime)
    {
        var buffer = new byte[1024 * 1024 * 4].AsMemory();

        while (_webSocket.State == WebSocketState.Open && lifetime.IsTerminated == false)
        {
            ValueWebSocketReceiveResult receiveResult;

            try
            {
                receiveResult = await _webSocket.ReceiveAsync(
                    buffer,
                    lifetime.Token
                );
            }
            catch (WebSocketException)
            {
                break;
            }

            if (_webSocket.CloseStatus != null)
                break;

            var payload = buffer[..receiveResult.Count];
            var context = MemoryPackSerializer.Deserialize<IServerRequest>(payload.Span)!;

            _received.Invoke(context);
        }
    }
}