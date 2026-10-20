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

        var reader = new WebSocketMessageReader();

        while (_webSocket.State == WebSocketState.Open && lifetime.IsTerminated == false)
        {
            WebSocketReadStatus status;

            try
            {
                status = await reader.Read(_webSocket, lifetime.Token);
            }
            catch (WebSocketException e) when (e.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
            {
                _logger.LogInformation("[Connection] Client disconnected without close handshake — closing connection");
                break;
            }
            catch (WebSocketException e)
            {
                _logger.LogError(e, "[Connection] WebSocket receive error — closing connection");
                break;
            }
            catch (OperationCanceledException e) when (lifetime.IsTerminated == false)
            {
                // Not cancelled by the server code: the socket was aborted - no pong within the keep-alive timeout
                // (a client lost without a close) or the host is stopping.
                _logger.LogInformation("[Connection] Socket aborted (keep-alive timeout or shutdown): {Reason} — closing connection",
                    e.InnerException?.Message ?? e.Message);
                break;
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("[Connection] Read loop cancelled — closing connection");
                break;
            }

            if (status == WebSocketReadStatus.Close)
            {
                _logger.LogInformation("[Connection] WebSocket close status received: {Status} {Description}",
                    _webSocket.CloseStatus,
                    _webSocket.CloseStatusDescription);
                break;
            }

            if (status == WebSocketReadStatus.TooBig)
            {
                _logger.LogError("[Connection] Message exceeds {Limit} bytes — closing connection",
                    WebSocketMessageReader.MaxMessageBytes);

                await CloseTooBig();
                break;
            }

            var payload = reader.Payload;

            IMessageFromClient? context;

            try
            {
                context = MemoryPackSerializer.Deserialize<IMessageFromClient>(payload.Span);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "[Connection] Failed to deserialize message — dropping payload ({Bytes} bytes)",
                    payload.Length);
                continue;
            }

            if (context == null)
            {
                _logger.LogWarning("[Connection] Deserialized null message — dropping payload ({Bytes} bytes)",
                    payload.Length);
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

    private async Task CloseTooBig()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        try
        {
            await _webSocket.CloseOutputAsync(WebSocketCloseStatus.MessageTooBig, "Message too big", timeout.Token);
        }
        catch (Exception e) when (e is WebSocketException or OperationCanceledException or ObjectDisposedException)
        {
            // The client may be gone already; the connection closes either way.
            _logger.LogWarning(e, "[Connection] Close after a too big message failed");
        }
    }
}
