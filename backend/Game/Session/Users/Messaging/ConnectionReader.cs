using System.Net.WebSockets;
using System.Threading.Channels;
using Common;
using MemoryPack;
using Shared;

namespace Game;

public interface IConnectionReader
{
    Channel<IServerRequest> Queue { get; }

    Task Run(ILifetime lifetime);
}

public class ConnectionReader : IConnectionReader
{
    public ConnectionReader(WebSocket webSocket, IExecutionQueue executionQueue)
    {
        _webSocket = webSocket;
        _executionQueue = executionQueue;
    }

    private readonly WebSocket _webSocket;
    private readonly IExecutionQueue _executionQueue;

    private readonly Channel<IServerRequest> _queue = Channel.CreateBounded<IServerRequest>(
        new BoundedChannelOptions(7)
        {
            SingleWriter = true,
            SingleReader = false,
            FullMode = BoundedChannelFullMode.Wait,
            AllowSynchronousContinuations = false
        });

    public Channel<IServerRequest> Queue => _queue;

    public async Task Run(ILifetime lifetime)
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

            await _queue.Writer.WriteAsync(context);
        }

        _executionQueue.Enqueue(lifetime.Terminate);
    }
}