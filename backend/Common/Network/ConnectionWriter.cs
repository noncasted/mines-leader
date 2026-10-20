using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Threading.Channels;
using Common.Reactive;
using MemoryPack;
using Microsoft.Extensions.Logging;
using Shared;

namespace Common.Network;

/// <summary>
/// Backend: WriteOneWay -> Client
/// Backend: WriteRequest -> Client: WriteResponse -> Backend: OnRequestHandled
/// Client: WriteRequest -> Backend: WriteResponse -> Client: OnRequestHandled
/// </summary>
public interface IConnectionWriter
{
    // Enqueue only, never waits and never throws: a message to a closed connection is dropped (the connection is
    // gone, its owner handles the loss), an overflow disconnects the slow client (logged once by the writer).
    void WriteOneWay(INetworkContext context);

    // The response T. Throws NetworkRequestException when the client answers with ErrorResponse,
    // InvalidOperationException on a response of another type, TimeoutException without a response,
    // OperationCanceledException when the connection is closed before the response.
    Task<T> WriteRequest<T>(INetworkContext context) where T : INetworkContext;

    // Same delivery as WriteOneWay.
    void WriteResponse(INetworkContext context, int requestId);

    void OnRequestHandled(INetworkContext context, int requestId);
}

public class ConnectionWriterOptions
{
    // Messages waiting for the socket. A client that lets this many pile up is too slow and gets disconnected:
    // producers (session ticks, messaging callbacks) never wait for a socket.
    public int QueueCapacity { get; init; } = 4096;

    // A request without a response in this time fails with TimeoutException.
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(30);
}

public class ConnectionOverflowException(int capacity)
    : Exception($"Connection send queue overflow: {capacity} messages are waiting for a slow client");

/// <summary>
/// Thread safe: Write* and OnRequestHandled are called from any thread; Run is the single reader that owns the socket.
/// </summary>
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

    private readonly ConcurrentDictionary<int, TaskCompletionSource<INetworkContext>> _pendingRequests = new();

    private int _requestId = 1_000_000;
    private int _isOverflowed;
    private int _isClosed;

    public int PendingRequestsCount => _pendingRequests.Count;

    public async Task Run(IReadOnlyLifetime lifetime)
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
            // The connection is closed: the loop ends, the queue and the pending requests are failed below.
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
                await _webSocket.SendAsync(sendBuffer, WebSocketMessageType.Binary, true, cancellation);
            }
            catch (OperationCanceledException) when (lifetime.IsTerminated == true)
            {
                throw;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "[Connection] Error while sending message: {Message}", message.GetType().FullName);

                if (message is RequestMessageFromServer request)
                    FailRequest(request.RequestId, e);
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

    public async Task<T> WriteRequest<T>(INetworkContext context) where T : INetworkContext
    {
        var requestId = Interlocked.Increment(ref _requestId);
        var completion = new TaskCompletionSource<INetworkContext>(TaskCreationOptions.RunContinuationsAsynchronously);

        if (_pendingRequests.TryAdd(requestId, completion) == false)
            throw new InvalidOperationException($"Request id {requestId} is already pending");

        try
        {
            var enqueueError = TryEnqueue(new RequestMessageFromServer()
            {
                Context = context,
                RequestId = requestId
            });

            if (enqueueError != null)
                throw enqueueError;

            var response = await completion.Task.WaitAsync(_options.RequestTimeout);

            return response switch
            {
                T typed => typed,
                ErrorResponse error => throw new NetworkRequestException(error.Code,
                    error.Message,
                    ErrorResponse.Describe(context)),
                _ => throw new InvalidOperationException(
                    $"Request {ErrorResponse.Describe(context)} got {response.GetType().FullName}, expected {typeof(T).FullName}")
            };
        }
        catch (TimeoutException)
        {
            throw new TimeoutException(
                $"Request {requestId} ({context.GetType().FullName}) got no response in {_options.RequestTimeout}");
        }
        finally
        {
            _pendingRequests.TryRemove(requestId, out _);
        }
    }

    public void WriteResponse(INetworkContext context, int requestId)
    {
        TryEnqueue(new ResponseMessageFromServer()
        {
            Context = context,
            RequestId = requestId
        });
    }

    public void OnRequestHandled(INetworkContext context, int requestId)
    {
        // A response racing with the timeout or with the connection close finds the request removed or completed.
        if (_pendingRequests.TryRemove(requestId, out var completion) == true && completion.TrySetResult(context) == true)
            return;

        _logger.LogWarning("[Connection] Response for unknown or completed request ID: {RequestId}", requestId);
    }

    // Null when the message is queued. A failure is already handled here: the overflow has disconnected the client
    // (logged once), a closed connection is the loss its owner handles. Only a request needs the error, to fail.
    private Exception? TryEnqueue(IMessageFromServer message)
    {
        if (_queue.Writer.TryWrite(message) == true)
            return null;

        if (Volatile.Read(ref _isOverflowed) == 1)
            return new ConnectionOverflowException(_options.QueueCapacity);

        // TryWrite on a bounded channel in Wait mode fails only when it is completed or full.
        if (Volatile.Read(ref _isClosed) == 1)
            return new OperationCanceledException("Connection is closed");

        OnOverflow();
        return new ConnectionOverflowException(_options.QueueCapacity);
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

    private void Shutdown()
    {
        Volatile.Write(ref _isClosed, 1);
        _queue.Writer.TryComplete();

        // Messages left in the queue are never sent: their requests fail now, not on the timeout.
        while (_queue.Reader.TryRead(out var message) == true)
        {
            if (message is RequestMessageFromServer request)
                FailRequest(request.RequestId, new OperationCanceledException("Connection is closed"));
        }

        foreach (var requestId in _pendingRequests.Keys)
        {
            if (_pendingRequests.TryRemove(requestId, out var completion) == true)
                completion.TrySetCanceled();
        }
    }

    private void FailRequest(int requestId, Exception exception)
    {
        if (_pendingRequests.TryRemove(requestId, out var completion) == true)
            completion.TrySetException(exception);
    }
}
