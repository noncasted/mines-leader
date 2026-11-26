using System.Net.WebSockets;

namespace Common.Network;

// Reads one whole WebSocket message: ReceiveAsync returns what arrived in one read from the stream, so a
// message comes in several parts (frames or partial TCP reads) and must be accumulated up to EndOfMessage.
// The buffer grows from InitialBytes up to the limit (MaxMessageBytes by default); a longer message is TooBig.
public sealed class WebSocketMessageReader
{
    public WebSocketMessageReader(int limit = MaxMessageBytes)
    {
        if (limit < 1)
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "The limit of a message must be positive");

        Limit = limit;
        _buffer = new byte[Math.Min(InitialBytes, limit)];
    }

    public const int InitialBytes = 64 * 1024;
    public const int MaxMessageBytes = 4 * 1024 * 1024;

    private byte[] _buffer;

    public int Limit { get; }

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
                if (_buffer.Length >= Limit)
                    return WebSocketReadStatus.TooBig;

                Array.Resize(ref _buffer, Math.Min(_buffer.Length * 2, Limit));
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
