using System.Net.WebSockets;
using System.Threading.Channels;
using Common;
using MemoryPack;
using Microsoft.Extensions.Logging;
using Shared;

namespace Game;

public class WebSocketWriter : IChannelWriter
{
    public WebSocketWriter(WebSocket webSocket, ILogger logger)
    {
        _webSocket = webSocket;
        _logger = logger;
    }

    private readonly WebSocket _webSocket;
    private readonly ILogger _logger;

    private readonly Channel<IServerResponse> _queue = Channel.CreateBounded<IServerResponse>(
        new BoundedChannelOptions(7)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait,
            AllowSynchronousContinuations = false
        });

    public async Task Run(IReadOnlyLifetime lifetime)
    {
        var reader = _queue.Reader;
        var buffer = new MemoryStream();
        var cancellation = lifetime.Token;

        while (await reader.WaitToReadAsync(cancellation) && IsAlive() == true)
        {
            while (reader.TryRead(out var message) == true && IsAlive() == true)
            {
                try
                {
                    await MemoryPackSerializer.SerializeAsync(buffer, message, cancellationToken: cancellation);

                    var sendBuffer = new ReadOnlyMemory<byte>(buffer.GetBuffer(), 0, (int)buffer.Position);
                    await _webSocket.SendAsync(sendBuffer, WebSocketMessageType.Binary, true, lifetime.Token);
                    buffer.Position = 0;
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Error while sending message: {Message}", message.GetType().FullName);
                }
            }
        }

        _queue.Writer.Complete();

        bool IsAlive()
        {
            return _webSocket.State == WebSocketState.Open && lifetime.IsTerminated == false;
        }
    }

    public ValueTask WriteEmpty(INetworkContext context)
    {
        var response = new ServerEmptyResponse()
        {
            Context = context
        };

        return _queue.Writer.WriteAsync(response);
    }

    public ValueTask WriteFull(INetworkContext context, int requestId)
    {
        var response = new ServerFullResponse()
        {
            Context = context,
            RequestId = requestId
        };

        return _queue.Writer.WriteAsync(response);
    }
}