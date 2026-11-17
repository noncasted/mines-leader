using System.Net.WebSockets;
using System.Threading.Channels;
using Internal;
using MemoryPack;
using Microsoft.Extensions.Logging;
using Shared;

namespace Common.Network;

// The server sends one-way messages and the responses to the requests of the client; it sends no requests of its own.
public interface IConnectionWriter
{
    // Enqueue only, never waits and never throws: a message to a closed connection is dropped (the connection is
    // gone, its owner handles the loss), an overflow disconnects the slow client (logged once by the writer).
    void WriteOneWay(INetworkContext context);

    // Same delivery as WriteOneWay.
    void WriteResponse(INetworkContext context, int requestId);
}

// Thread safe: Write* are called from any thread; Run is the single reader that owns the socket.
public class ConnectionWriter : IConnectionWriter
{
    public ConnectionWriter(WebSocket webSocket, ILogger logger, ConnectionWriterOptions? options = null)
    {
        _webSocket = webSocket;
        _logger = logger;
        _options = options ?? new ConnectionWriterOptions();

        _queue = Channel.CreateBounded<IMessageFromServer>(new BoundedChannelOptions(_options.QueueCapacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait,
            // Producers must not run the send loop inline on their own thread.
            AllowSynchronousContinuations = false
        });
    }

    private readonly WebSocket _webSocket;
    private readonly ILogger _logger;
    private readonly ConnectionWriterOptions _options;
    private readonly Channel<IMessageFromServer> _queue;

    private int _isOverflowed;
    private int _isClosed;

    // The end of lifetime stops the loop after the send in flight. The end of socketLifetime cancels that send, which
    // aborts the socket: it comes after the close of the socket, or the close frame dies with it.
    public async Task Run(IReadOnlyLifetime lifetime, IReadOnlyLifetime socketLifetime)
    {
        var reader = _queue.Reader;
        var buffer = new MemoryStream();
        var cancellation = lifetime.Token;

        try
        {
            while (IsAlive() == true)
            {
                if (await reader.WaitToReadAsync(cancellation) == false)
                    break;

                while (IsAlive() == true && reader.TryRead(out var message) == true)
                    await Send(message);
            }
        }
        catch (OperationCanceledException) when (lifetime.IsTerminated == true)
        {
            // The connection is closed: the loop ends, the messages left in the queue are dropped below.
        }
        finally
        {
            Shutdown();
            await buffer.DisposeAsync();
        }

        async Task Send(IMessageFromServer message)
        {
            try
            {
                await MemoryPackSerializer.SerializeAsync(buffer, message, cancellationToken: cancellation);

                var sendBuffer = new ReadOnlyMemory<byte>(buffer.GetBuffer(), 0, (int)buffer.Position);
                await _webSocket.SendAsync(sendBuffer, WebSocketMessageType.Binary, true, socketLifetime.Token);
            }
            catch (OperationCanceledException) when (lifetime.IsTerminated == true)
            {
                throw;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "[Connection] Error while sending message: {Message}", message.GetType().FullName);
            }
            finally
            {
                // A failed serialization or send leaves the position mid-message: the next message must
                // start from zero, not after the leftovers.
                buffer.Position = 0;
            }
        }

        bool IsAlive()
        {
            return _webSocket.State == WebSocketState.Open &&
                   lifetime.IsTerminated == false &&
                   Volatile.Read(ref _isOverflowed) == 0;
        }
    }

    public void WriteOneWay(INetworkContext context)
    {
        TryEnqueue(new OneWayMessageFromServer()
        {
            Context = context
        });
    }

    public void WriteResponse(INetworkContext context, int requestId)
    {
        TryEnqueue(new ResponseMessageFromServer()
        {
            Context = context,
            RequestId = requestId
        });
    }

    // A message to a closed connection is the loss its owner handles; a full queue disconnects the slow client.
    private void TryEnqueue(IMessageFromServer message)
    {
        if (_queue.Writer.TryWrite(message) == true)
            return;

        // TryWrite on a bounded channel in Wait mode fails only when it is completed or full.
        if (Volatile.Read(ref _isOverflowed) == 1 || Volatile.Read(ref _isClosed) == 1)
            return;

        OnOverflow();
    }

    private void OnOverflow()
    {
        if (Interlocked.Exchange(ref _isOverflowed, 1) == 1)
            return;

        _logger.LogError("[Connection] Send queue overflow ({Capacity} messages) — disconnecting slow client",
            _options.QueueCapacity);

        // Abort unblocks a send stuck on the slow client and ends the read loop: Connection.Run then terminates
        // the connection lifetime on its own thread. The send loop stops on the overflow flag.
        _webSocket.Abort();
        _queue.Writer.TryComplete(new ConnectionOverflowException(_options.QueueCapacity));
    }

    // Messages left in the queue are never sent.
    private void Shutdown()
    {
        Volatile.Write(ref _isClosed, 1);
        _queue.Writer.TryComplete();
    }
}
