using System.Net.WebSockets;

namespace Common.Network;

public enum WebSocketReadStatus
{
    Message,
    Close,
    TooBig
}

// Reads one whole WebSocket message: ReceiveAsync returns what arrived in one read from the stream, so a
// message comes in several parts (frames or partial TCP reads) and must be accumulated up to EndOfMessage.
// The buffer grows from InitialBytes up to MaxMessageBytes; a longer message is TooBig.
public sealed class WebSocketMessageReader
{
    public const int InitialBytes = 64 * 1024;
    public const int MaxMessageBytes = 4 * 1024 * 1024;

    private byte[] _buffer = new byte[InitialBytes];

    // Valid until the next Read.
    public ReadOnlyMemory<byte> Payload { get; private set; }

    public async Task<WebSocketReadStatus> Read(WebSocket webSocket, CancellationToken cancellation)
    {
        var count = 0;
        Payload = ReadOnlyMemory<byte>.Empty;

        while (true)
        {
            if (count == _buffer.Length)
            {
                if (_buffer.Length >= MaxMessageBytes)
                    return WebSocketReadStatus.TooBig;

                Array.Resize(ref _buffer, Math.Min(_buffer.Length * 2, MaxMessageBytes));
            }

            var result = await webSocket.ReceiveAsync(_buffer.AsMemory(count), cancellation);

            if (result.MessageType == WebSocketMessageType.Close)
                return WebSocketReadStatus.Close;

            count += result.Count;

            if (result.EndOfMessage == true)
            {
                Payload = _buffer.AsMemory(0, count);
                return WebSocketReadStatus.Message;
            }
        }
    }
}
