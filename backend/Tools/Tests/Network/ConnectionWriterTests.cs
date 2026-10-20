using System.Net.WebSockets;
using System.Threading.Channels;
using Common.Network;
using Common.Reactive;
using FluentAssertions;
using MemoryPack;
using Microsoft.Extensions.Logging.Abstractions;
using Shared;
using Xunit;

namespace Tests.Network;

/// <summary>
/// F2.4: backend ConnectionWriter - unique request ids under concurrency, response/timeout race, request timeout,
/// queue completion on close, slow client disconnect on overflow.
/// </summary>
public class ConnectionWriterTests
{
    static ConnectionWriterTests()
    {
        NetworkContextsSetup.Register();
    }

    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task ConcurrentRequests_GetUniqueIds_AndAllComplete()
    {
        var socket = new FakeWebSocket();
        var lifetime = new Lifetime();
        var writer = new ConnectionWriter(socket, NullLogger.Instance);
        var run = writer.Run(lifetime);

        const int count = 200;

        // Client side: answers every request with a response carrying the request id.
        var responder = Task.Run(async () => {
            for (var i = 0; i < count; i++)
            {
                var message = await socket.ReadSent(Wait);
                var request = message.Should().BeOfType<RequestMessageFromServer>().Subject;
                writer.OnRequestHandled(new SharedSessionPlayer.RemoteDisconnect { Index = request.RequestId }, request.RequestId);
            }
        });

        var requests = Enumerable.Range(0, count)
                                 .Select(_ => Task.Run(() => writer.WriteRequest<SharedSessionPlayer.RemoteDisconnect>(
                                     new SharedSessionPlayer.LocalUpdate())))
                                 .ToArray();

        var responses = await Task.WhenAll(requests).WaitAsync(Wait);
        await responder.WaitAsync(Wait);

        responses.Select(r => ((SharedSessionPlayer.RemoteDisconnect)r!).Index).Should().OnlyHaveUniqueItems().And.HaveCount(count);
        writer.PendingRequestsCount.Should().Be(0);

        lifetime.Terminate();
        await run.WaitAsync(Wait);
    }

    [Fact]
    public async Task Request_WithoutResponse_TimesOut_AndLateResponseIsIgnored()
    {
        var socket = new FakeWebSocket();
        var lifetime = new Lifetime();
        var writer = new ConnectionWriter(socket, NullLogger.Instance, new ConnectionWriterOptions
        {
            RequestTimeout = TimeSpan.FromMilliseconds(200)
        });
        var run = writer.Run(lifetime);

        var request = writer.WriteRequest<SharedSessionPlayer.RemoteDisconnect>(new SharedSessionPlayer.LocalUpdate());
        var sent = (RequestMessageFromServer)await socket.ReadSent(Wait);

        await request.Invoking(r => r.WaitAsync(Wait)).Should().ThrowAsync<TimeoutException>();
        writer.PendingRequestsCount.Should().Be(0);

        // The response after the timeout races with the removal: it must not throw.
        writer.Invoking(w => w.OnRequestHandled(new SharedSessionPlayer.RemoteDisconnect(), sent.RequestId)).Should().NotThrow();

        lifetime.Terminate();
        await run.WaitAsync(Wait);
    }

    [Fact]
    public async Task ResponseRacingWithClose_DoesNotThrow()
    {
        for (var i = 0; i < 50; i++)
        {
            var socket = new FakeWebSocket();
            var lifetime = new Lifetime();
            var writer = new ConnectionWriter(socket, NullLogger.Instance);
            var run = writer.Run(lifetime);

            var request = writer.WriteRequest<SharedSessionPlayer.RemoteDisconnect>(new SharedSessionPlayer.LocalUpdate());
            var sent = (RequestMessageFromServer)await socket.ReadSent(Wait);

            var respond = Task.Run(() => writer.OnRequestHandled(new SharedSessionPlayer.RemoteDisconnect(), sent.RequestId));
            lifetime.Terminate();

            await respond.WaitAsync(Wait);
            await run.WaitAsync(Wait);

            // Either the response or the close wins, never both and never a hang.
            var completed = await Task.WhenAny(request, Task.Delay(Wait));
            completed.Should().BeSameAs(request);
            request.IsCompletedSuccessfully.Should().Be(request.IsCanceled == false);
        }
    }

    [Fact]
    public async Task Close_CompletesQueue_PendingRequestsCanceled_WritesFail()
    {
        var socket = new FakeWebSocket();
        var lifetime = new Lifetime();
        var writer = new ConnectionWriter(socket, NullLogger.Instance);
        var run = writer.Run(lifetime);

        var request = writer.WriteRequest<SharedSessionPlayer.RemoteDisconnect>(new SharedSessionPlayer.LocalUpdate());
        await socket.ReadSent(Wait);

        lifetime.Terminate();
        await run.WaitAsync(Wait);

        await request.Invoking(r => r.WaitAsync(Wait)).Should().ThrowAsync<OperationCanceledException>();
        writer.PendingRequestsCount.Should().Be(0);

        // Before the fix the loop left the queue open: producers on a full queue waited forever.
        await writer.Invoking(w => w.WriteOneWay(new SharedSessionPlayer.RemoteDisconnect()).AsTask().WaitAsync(Wait))
                    .Should().ThrowAsync<ChannelClosedException>();
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
        var run = writer.Run(lifetime);

        // The first message is taken by the stuck send, the next `capacity` fill the queue; every write returns
        // immediately instead of waiting for the socket.
        var writes = new List<ValueTask> { writer.WriteOneWay(new SharedSessionPlayer.RemoteDisconnect { Index = 0 }) };
        await socket.WaitForBlockedSend(Wait);

        for (var i = 1; i <= capacity; i++)
            writes.Add(writer.WriteOneWay(new SharedSessionPlayer.RemoteDisconnect { Index = i }));

        var overflow = writer.WriteOneWay(new SharedSessionPlayer.RemoteDisconnect { Index = -1 });
        overflow.IsCompleted.Should().BeTrue();
        await overflow.Invoking(o => o.AsTask()).Should().ThrowAsync<ConnectionOverflowException>();

        socket.State.Should().Be(WebSocketState.Aborted);
        await run.WaitAsync(Wait);

        // Once overflowed, further writes fail fast as well.
        await writer.Invoking(w => w.WriteOneWay(new SharedSessionPlayer.RemoteDisconnect()).AsTask())
                    .Should().ThrowAsync<ConnectionOverflowException>();

        foreach (var write in writes)
            write.IsCompletedSuccessfully.Should().BeTrue();

        lifetime.Terminate();
    }

    [Fact]
    public async Task SendFailure_FailsRequest_InsteadOfNull()
    {
        var socket = new FakeWebSocket { SendFailure = new WebSocketException("send failed") };
        var lifetime = new Lifetime();
        var writer = new ConnectionWriter(socket, NullLogger.Instance);
        var run = writer.Run(lifetime);

        var request = writer.WriteRequest<SharedSessionPlayer.RemoteDisconnect>(new SharedSessionPlayer.LocalUpdate());

        await request.Invoking(r => r.WaitAsync(Wait)).Should().ThrowAsync<WebSocketException>();
        writer.PendingRequestsCount.Should().Be(0);

        lifetime.Terminate();
        await run.WaitAsync(Wait);
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
