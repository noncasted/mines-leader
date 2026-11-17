using System.Net.WebSockets;
using Internal;
using MemoryPack;
using Microsoft.Extensions.Logging;
using Shared;

namespace Common.Network;

public interface IConnectionReader
{
    IViewableDelegate<OneWayMessageFromClient> OneWay { get; }
    IViewableDelegate<RequestMessageFromClient> Requests { get; }
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

    public IViewableDelegate<OneWayMessageFromClient> OneWay => _oneWay;
    public IViewableDelegate<RequestMessageFromClient> Requests => _requests;

    // Connection.Run starts reading only with a handler: a message read before it would be lost.
    public bool HasHandlers => _oneWay.ListenersCount + _requests.ListenersCount > 0;

    public async Task Run(IReadOnlyLifetime lifetime)
    {
        if (lifetime.IsTerminated == true)
            throw new InvalidOperationException("Connection is terminated");

        var reader = new WebSocketMessageReader();

        // After the close of the server (CloseSent) the loop reads on to the close of the client: a socket disposed
        // before it resets the connection. The wait is bounded by the close timeout of Connection (lifetime).
        while (_webSocket.State is (WebSocketState.Open or WebSocketState.CloseSent) && lifetime.IsTerminated == false)
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

            // The server has closed: what the client sent before its answer is not handled (nor limited: the close is made).
            if (_webSocket.State == WebSocketState.CloseSent)
                continue;

            if (status == WebSocketReadStatus.TooBig)
            {
                _logger.LogError("[Connection] Message exceeds {Limit} bytes — closing connection", reader.Limit);
                await Close(WebSocketCloseStatus.MessageTooBig, "Message too big");
                break;
            }

            var refusal = Dispatch(reader.Payload);

            if (refusal != null)
            {
                await Close(refusal.Value, refusal == WebSocketCloseStatus.InvalidPayloadData
                    ? "Unreadable message"
                    : "Unexpected message");
                break;
            }
        }
    }

    // The close status when the message is not one the server takes: the connection is closed, a dropped request would
    // leave the client waiting for its response.
    private WebSocketCloseStatus? Dispatch(ReadOnlyMemory<byte> payload)
    {
        IMessageFromClient? message;

        try
        {
            message = MemoryPackSerializer.Deserialize<IMessageFromClient>(payload.Span);
        }
        catch (Exception e)
        {
            // The bytes of the client are untrusted: whatever the deserializer throws on them is a broken message.
            _logger.LogError(e, "[Connection] Failed to deserialize message ({Bytes} bytes) — closing connection",
                payload.Length);
            return WebSocketCloseStatus.InvalidPayloadData;
        }

        switch (message)
        {
            case OneWayMessageFromClient oneWay:
                _oneWay.Invoke(oneWay);
                return null;
            case RequestMessageFromClient request:
                _requests.Invoke(request);
                return null;
            default:
                // Null, or a response: the server sends no requests, so a response answers nothing.
                _logger.LogError("[Connection] Unexpected message {Type} ({Bytes} bytes) — closing connection",
                    message?.GetType().FullName ?? "null",
                    payload.Length);
                return WebSocketCloseStatus.ProtocolError;
        }
    }

    private async Task Close(WebSocketCloseStatus status, string description)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        try
        {
            await _webSocket.CloseOutputAsync(status, description, timeout.Token);
        }
        catch (Exception e) when (e is WebSocketException or OperationCanceledException or ObjectDisposedException)
        {
            // The client may be gone already; the connection closes either way.
            _logger.LogWarning(e, "[Connection] Close with {Status} failed", status);
        }
    }
}
