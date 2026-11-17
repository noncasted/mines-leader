using System.Net.WebSockets;
using Common.Network;
using MemoryPack;
using Shared;

namespace Framework.Tests.ClientTelemetry;

// A client of TelemetryMetaSocket over a real websocket, one request at a time.
public sealed class TelemetryMetaClient : IAsyncDisposable
{
    private readonly ClientWebSocket _socket = new();
    private readonly WebSocketMessageReader _reader = new();
    private int _requestId;

    public static async Task<TelemetryMetaClient> Connect(Uri host)
    {
        var client = new TelemetryMetaClient();
        await client._socket.ConnectAsync(new UriBuilder(host) { Scheme = "ws", Path = TelemetryMetaSocket.Route }.Uri, CancellationToken.None);
        return client;
    }

    public async Task<T> Request<T>(INetworkContext request) where T : INetworkContext
    {
        var id = ++_requestId;
        var bytes = MemoryPackSerializer.Serialize<IMessageFromClient>(new RequestMessageFromClient { Context = request, RequestId = id });

        await _socket.SendAsync(bytes, WebSocketMessageType.Binary, true, CancellationToken.None);

        if (await _reader.Read(_socket, CancellationToken.None) != WebSocketReadStatus.Message)
            throw new InvalidOperationException("The socket closed before the answer");

        var response = (ResponseMessageFromServer)MemoryPackSerializer.Deserialize<IMessageFromServer>(_reader.Payload.Span)!;
        return response.RequestId == id ? (T)response.Context : throw new InvalidOperationException($"An answer to {response.RequestId}, expected {id}");
    }

    // A close from the side of the client, as a tab that went away in order.
    public async ValueTask DisposeAsync()
    {
        if (_socket.State == WebSocketState.Open)
            await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);

        _socket.Dispose();
    }
}
