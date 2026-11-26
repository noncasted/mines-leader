using System.Net.WebSockets;
using System.Threading.Channels;
using Common.Network;
using Internal;
using FluentAssertions;
using MemoryPack;
using Microsoft.Extensions.Logging.Abstractions;
using Shared;
using Xunit;
using ErrorCode = Shared.ErrorCode;

namespace Framework.Tests.Network;

// F2.4: backend ConnectionWriter - queue completion on close, slow client disconnect on overflow.
public class ConnectionWriterTests
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task Close_CompletesQueue_WritesDropped()
    {
        var socket = new FakeWebSocket();
        var lifetime = new Lifetime();
        var writer = new ConnectionWriter(socket, NullLogger.Instance);
        var run = writer.Run(lifetime, lifetime);

        writer.WriteOneWay(new SharedSessionLatency.Pong());
        await socket.ReadSent(Wait);

        lifetime.Terminate();
        await run.WaitAsync(Wait, TestContext.Current.CancellationToken);

        // Before the fix the loop left the queue open: producers on a full queue waited forever. A write to a closed
        // connection is dropped without an error (the loss is handled by the owner).
        writer.Invoking(w => w.WriteOneWay(new SharedSessionLatency.Pong())).Should().NotThrow();
        writer.Invoking(w => w.WriteResponse(new SharedSessionLatency.Pong(), 1)).Should().NotThrow();
    }

    [Fact]
    public async Task SlowClient_Overflow_AbortsSocket_ProducersNeverBlock()
    {
        const int capacity = 8;

        var socket = new FakeWebSocket { BlockSends = true };
        var lifetime = new Lifetime();
        var writer = new ConnectionWriter(socket, NullLogger.Instance, new ConnectionWriterOptions
        {
            QueueCapacity = capacity
        });
        var run = writer.Run(lifetime, lifetime);

        // The first message is taken by the stuck send, the next `capacity` fill the queue; every write returns
        // immediately instead of waiting for the socket.
        writer.WriteOneWay(new SharedSessionLatency.Pong { Id = 0 });
        await socket.WaitForBlockedSend(Wait);

        for (var i = 1; i <= capacity; i++)
            writer.WriteOneWay(new SharedSessionLatency.Pong { Id = i });

        socket.State.Should().Be(WebSocketState.Open);

        // The overflow disconnects the client inside the writer: the caller (a session tick, a messaging callback)
        // neither waits nor throws.
        writer.Invoking(w => w.WriteOneWay(new SharedSessionLatency.Pong { Id = -1 })).Should().NotThrow();

        socket.State.Should().Be(WebSocketState.Aborted);
        await run.WaitAsync(Wait, TestContext.Current.CancellationToken);

        writer.Invoking(w => w.WriteOneWay(new SharedSessionLatency.Pong())).Should().NotThrow();

        lifetime.Terminate();
    }


    private class FakeWebSocket : WebSocket
    {
        private readonly Channel<byte[]> _sent = Channel.CreateUnbounded<byte[]>();
        private readonly TaskCompletionSource _sendBlocked = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly CancellationTokenSource _abort = new();

        private WebSocketState _state = WebSocketState.Open;

        public bool BlockSends { get; init; }
        public Exception? SendFailure { get; init; }

        public override WebSocketCloseStatus? CloseStatus => null;
        public override string? CloseStatusDescription => null;
        public override WebSocketState State => _state;
        public override string? SubProtocol => null;

        public async Task<IMessageFromServer> ReadSent(TimeSpan timeout)
        {
            var bytes = await _sent.Reader.ReadAsync().AsTask().WaitAsync(timeout);
            return MemoryPackSerializer.Deserialize<IMessageFromServer>(bytes)!;
        }

        public Task WaitForBlockedSend(TimeSpan timeout)
        {
            return _sendBlocked.Task.WaitAsync(timeout);
        }

        public override async ValueTask SendAsync(
            ReadOnlyMemory<byte> buffer,
            WebSocketMessageType messageType,
            bool endOfMessage,
            CancellationToken cancellationToken)
        {
            if (SendFailure != null)
                throw SendFailure;

            if (BlockSends == true)
            {
                _sendBlocked.TrySetResult();

                using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _abort.Token);
                await Task.Delay(Timeout.Infinite, linked.Token);
            }

            _sent.Writer.TryWrite(buffer.ToArray());
        }

        public override Task SendAsync(
            ArraySegment<byte> buffer,
            WebSocketMessageType messageType,
            bool endOfMessage,
            CancellationToken cancellationToken)
        {
            return SendAsync(buffer.AsMemory(), messageType, endOfMessage, cancellationToken).AsTask();
        }

        public override Task<WebSocketReceiveResult> ReceiveAsync(
            ArraySegment<byte> buffer,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public override void Abort()
        {
            _state = WebSocketState.Aborted;
            _abort.Cancel();
        }

        public override Task CloseAsync(
            WebSocketCloseStatus closeStatus,
            string? statusDescription,
            CancellationToken cancellationToken)
        {
            _state = WebSocketState.Closed;
            return Task.CompletedTask;
        }

        public override Task CloseOutputAsync(
            WebSocketCloseStatus closeStatus,
            string? statusDescription,
            CancellationToken cancellationToken)
        {
            _state = WebSocketState.CloseSent;
            return Task.CompletedTask;
        }

        public override void Dispose()
        {
            _abort.Dispose();
        }
    }
}
