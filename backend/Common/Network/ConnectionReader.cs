using System.Net.WebSockets;
using Common.Reactive;
using MemoryPack;
using Microsoft.Extensions.Logging;
using Shared;

namespace Common.Network;

public interface IConnectionReader
{
    IViewableDelegate<OneWayMessageFromClient> OneWay { get; }
    IViewableDelegate<RequestMessageFromClient> Requests { get; }
    IViewableDelegate<ResponseMessageFromClient> Responses { get; }
}

public class ConnectionReader : IConnectionReader
{
    public ConnectionReader(WebSocket webSocket, ILogger logger)
    {
        _webSocket = webSocket;
        _logger = logger;
    }

    private readonly WebSocket _webSocket;
    private readonly ILogger _logger;

    private readonly ViewableDelegate<OneWayMessageFromClient> _oneWay = new();
    private readonly ViewableDelegate<RequestMessageFromClient> _requests = new();
    private readonly ViewableDelegate<ResponseMessageFromClient> _responses = new();

    public IViewableDelegate<OneWayMessageFromClient> OneWay => _oneWay;
    public IViewableDelegate<RequestMessageFromClient> Requests => _requests;
    public IViewableDelegate<ResponseMessageFromClient> Responses => _responses;

    public async Task Run(IReadOnlyLifetime lifetime)
    {
        if (lifetime.IsTerminated == true)
            throw new InvalidOperationException("Connection is terminated");

        var buffer = new byte[1024 * 1024 * 4].AsMemory();

        while (_webSocket.State == WebSocketState.Open && lifetime.IsTerminated == false)
        {
            ValueWebSocketReceiveResult receiveResult;

            try
            {
                receiveResult = await _webSocket.ReceiveAsync(buffer, lifetime.Token);
            }
            catch (WebSocketException e)
            {
                _logger.LogError(e, "[Connection] WebSocket receive error — closing connection");
                break;
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("[Connection] Read loop cancelled — closing connection");
                break;
            }

            if (_webSocket.CloseStatus != null)
            {
                _logger.LogInformation("[Connection] WebSocket close status received: {Status} {Description}",
                    _webSocket.CloseStatus,
                    _webSocket.CloseStatusDescription);
                break;
            }

            var payload = buffer[..receiveResult.Count];

            IMessageFromClient? context;

            try
            {
                context = MemoryPackSerializer.Deserialize<IMessageFromClient>(payload.Span);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "[Connection] Failed to deserialize message — dropping payload ({Bytes} bytes)",
                    receiveResult.Count);
                continue;
            }

            if (context == null)
            {
                _logger.LogWarning("[Connection] Deserialized null message — dropping payload ({Bytes} bytes)",
                    receiveResult.Count);
                continue;
            }

            switch (context)
            {
                case OneWayMessageFromClient oneWay:
                    _oneWay.Invoke(oneWay);
                    break;
                case RequestMessageFromClient request:
                    _requests.Invoke(request);
                    break;
                case ResponseMessageFromClient response:
                    _responses.Invoke(response);
                    break;
                default:
                    _logger.LogWarning("[Connection] Unknown message type received: {Type} — dropping",
                        context.GetType().FullName);
                    break;
            }
        }
    }
}
