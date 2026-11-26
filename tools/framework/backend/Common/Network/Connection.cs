using System.Net.WebSockets;
using Common.Extensions;
using Internal;
using Microsoft.Extensions.Logging;

namespace Common.Network;

public interface IConnection
{
    IReadOnlyLifetime Lifetime { get; }
    IConnectionReader Reader { get; }
    IConnectionWriter Writer { get; }

    // Completes once the socket is closed: the close frame went out or the client is gone. The end of Lifetime closes the
    // socket, run or not. The owner of the socket (the handler of its request) returns only after it, a socket that ends
    // with its handler takes the close frame with it.
    Task Closed { get; }

    // Reads the socket until the connection ends and completes after Closed. The handlers of Reader are advised before:
    // a message read before them would be lost, so a run without a handler throws. A connection ended before the run is
    // an ordinary disconnect: the run completes after its close.
    Task Run();

    void ForceDisconnect();
}

public class Connection : IConnection
{
    public Connection(WebSocket webSocket, IReadOnlyLifetime parentLifetime, ILogger logger)
    {
        _webSocket = webSocket;
        _logger = logger;
        _lifetime = parentLifetime.Child();
        _reader = new ConnectionReader(webSocket, logger);
        _writer = new ConnectionWriter(webSocket, logger);

        // Not in Run: a connection its owner never runs (a session aborted before its start) is closed all the same.
        _lifetime.Listen(OnDisconnected);
    }

    private readonly WebSocket _webSocket;
    private readonly ILogger _logger;
    private static readonly TimeSpan CloseTimeout = TimeSpan.FromSeconds(2);

    private readonly ILifetime _lifetime;

    // The ReceiveAsync and the SendAsync in flight are cancelled only after the close: cancelling either aborts the socket
    // (Kestrel), and a disconnect reached the client as 1006 instead of a close. So it is no child of the parent: the end
    // of the parent reaches the socket through the close (_lifetime), as a forced disconnect does. The timeout of the
    // close bounds a send stuck on a client that does not read: the close frame waits behind it.
    private readonly ILifetime _socketLifetime = new Lifetime();

    private readonly ConnectionReader _reader;
    private readonly ConnectionWriter _writer;

    // The read loop of Run is over, or will never start. A close that comes after Run has claimed the read waits for it:
    // the loop may be about to receive, and a receive with a cancelled token aborts the socket.
    private readonly TaskCompletionSource _readEnded = new(TaskCreationOptions.RunContinuationsAsynchronously);

    // The close is over (Closed). Run ends after it.
    private readonly TaskCompletionSource _closed = new(TaskCreationOptions.RunContinuationsAsynchronously);

    // Who came first, Run (Reading) or the close (Unread): a close before the run knows that no loop will read.
    private int _readState = ReadPending;
    private int _isClosing;

    private const int ReadPending = 0;
    private const int Reading = 1;
    private const int Unread = 2;

    public IReadOnlyLifetime Lifetime => _lifetime;
    public IConnectionReader Reader => _reader;
    public IConnectionWriter Writer => _writer;
    public Task Closed => _closed.Task;

    public async Task Run()
    {
        // Neither reads: the close is still made, so Closed completes for the owner either way.
        if (_lifetime.IsTerminated == false && _reader.HasHandlers == false)
        {
            ForceDisconnect();
            await _closed.Task;

            throw new InvalidOperationException("Connection.Run without a handler of its reader: advise Reader first");
        }

        // Ended before the run: the close is made without a read.
        if (Interlocked.CompareExchange(ref _readState, Reading, ReadPending) != ReadPending)
        {
            await _closed.Task;
            return;
        }

        _writer.Run(_lifetime, _socketLifetime).NoAwait();

        // A failure of the read loop (a handler of a message that throws) ends the connection too: its lifetime and
        // the socket are not left open behind the exception.
        try
        {
            await _reader.Run(_socketLifetime);
        }
        finally
        {
            _readEnded.TrySetResult();
            _lifetime.Terminate();
            await _closed.Task;
        }
    }

    public void ForceDisconnect()
    {
        _lifetime.Terminate();
    }

    private void OnDisconnected()
    {
        if (Interlocked.Exchange(ref _isClosing, 1) == 1)
            return;

        if (Interlocked.CompareExchange(ref _readState, Unread, ReadPending) == ReadPending)
            _readEnded.TrySetResult();

        Close().NoAwait();
    }

    private async Task Close()
    {
        try
        {
            if (_webSocket.State is not (WebSocketState.Open or WebSocketState.CloseReceived))
                return;

            // The close frame goes out next to the pending receive; the receive gets the answer of the client and the
            // read loop ends. A client that does not answer is cut after the timeout.
            using var timeout = new CancellationTokenSource(CloseTimeout);
            await _webSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "User disconnected", timeout.Token);
            await Task.WhenAny(_readEnded.Task, Task.Delay(CloseTimeout));
        }
        catch (Exception e) when (e is WebSocketException or ObjectDisposedException or IOException
                                      or OperationCanceledException)
        {
            // The client may already be gone without completing the handshake: the connection ends without the frame.
            _logger.LogInformation("[Connection] Close frame not delivered: {Reason}", e.Message);
        }
        finally
        {
            _closed.TrySetResult();
            _socketLifetime.Terminate();
        }
    }
}
