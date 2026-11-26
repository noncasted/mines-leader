using System.Net.WebSockets;

namespace Framework.Tests.Network;

// Returns the queued parts one per ReceiveAsync, cut to the free space of the buffer like a real socket.
internal sealed class ScriptedSocket : WebSocket
{
    private readonly Queue<(byte[] Bytes, bool EndOfMessage, bool IsClose)> _parts = new();
    private WebSocketState _state = WebSocketState.Open;

    // The status of the close frame the server sent, null before it.
    public WebSocketCloseStatus? SentCloseStatus { get; private set; }

    // The parts not received yet.
    public int PendingParts => _parts.Count;

    public override WebSocketCloseStatus? CloseStatus => null;
    public override string? CloseStatusDescription => null;
    public override WebSocketState State => _state;
    public override string? SubProtocol => null;

    public void Enqueue(byte[] bytes, bool endOfMessage)
    {
        _parts.Enqueue((bytes, endOfMessage, false));
    }

    public void EnqueueClose()
    {
        _parts.Enqueue(([], true, true));
    }

    public override Task<WebSocketReceiveResult> ReceiveAsync(
        ArraySegment<byte> buffer,
        CancellationToken cancellationToken)
    {
        var (bytes, endOfMessage, isClose) = _parts.Dequeue();

        if (isClose == true)
        {
            _state = WebSocketState.CloseReceived;
            return Task.FromResult(new WebSocketReceiveResult(0, WebSocketMessageType.Close, true));
        }

        var count = Math.Min(bytes.Length, buffer.Count);
        bytes.AsSpan(0, count).CopyTo(buffer.AsSpan());

        // What did not fit stays for the next read, as the rest of the frame.
        if (count < bytes.Length)
        {
            var rest = new Queue<(byte[], bool, bool)>();
            rest.Enqueue((bytes[count..], endOfMessage, false));

            while (_parts.Count > 0)
                rest.Enqueue(_parts.Dequeue());

            while (rest.Count > 0)
                _parts.Enqueue(rest.Dequeue());

            return Task.FromResult(new WebSocketReceiveResult(count, WebSocketMessageType.Binary, false));
        }

        return Task.FromResult(new WebSocketReceiveResult(count, WebSocketMessageType.Binary, endOfMessage));
    }

    public override Task SendAsync(
        ArraySegment<byte> buffer,
        WebSocketMessageType messageType,
        bool endOfMessage,
        CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public override Task CloseAsync(
        WebSocketCloseStatus closeStatus,
        string? statusDescription,
        CancellationToken cancellationToken)
    {
        SentCloseStatus = closeStatus;
        _state = WebSocketState.Closed;
        return Task.CompletedTask;
    }

    public override Task CloseOutputAsync(
        WebSocketCloseStatus closeStatus,
        string? statusDescription,
        CancellationToken cancellationToken)
    {
        SentCloseStatus = closeStatus;
        _state = WebSocketState.CloseSent;
        return Task.CompletedTask;
    }

    public override void Abort()
    {
        _state = WebSocketState.Aborted;
    }

    public override void Dispose()
    {
    }
}
