using System.Net.WebSockets;
using Common.Network;
using Common.Reactive;
using FluentAssertions;
using MemoryPack;
using Microsoft.Extensions.Logging.Abstractions;
using Shared;
using Xunit;

namespace Tests.Network;

/// <summary>
/// F8.5: WebSocketMessageReader assembles a message from its parts and limits its size; ConnectionReader routes
/// the messages by kind and drops a payload it cannot read without ending the connection.
/// </summary>
public class ConnectionReaderTests
{
    [Fact]
    public async Task MessageReader_PartsAreJoinedUpToEndOfMessage()
    {
        var socket = new ScriptedSocket();
        socket.Enqueue([1, 2], endOfMessage: false);
        socket.Enqueue([3], endOfMessage: false);
        socket.Enqueue([4, 5], endOfMessage: true);
        var reader = new WebSocketMessageReader();

        var status = await reader.Read(socket, CancellationToken.None);

        status.Should().Be(WebSocketReadStatus.Message);
        reader.Payload.ToArray().Should().Equal(1, 2, 3, 4, 5);
    }

    [Fact]
    public async Task MessageReader_MessageOverInitialBuffer_GrowsAndReadsWhole()
    {
        var socket = new ScriptedSocket();
        var big = Enumerable.Range(0, WebSocketMessageReader.InitialBytes * 3).Select(i => (byte)i).ToArray();
        socket.Enqueue(big, endOfMessage: true);
        var reader = new WebSocketMessageReader();

        var status = await reader.Read(socket, CancellationToken.None);

        status.Should().Be(WebSocketReadStatus.Message);
        reader.Payload.ToArray().Should().Equal(big);
    }

    [Fact]
    public async Task MessageReader_MessageOverLimit_IsTooBig()
    {
        var socket = new ScriptedSocket();
        socket.Enqueue(new byte[WebSocketMessageReader.MaxMessageBytes + 1], endOfMessage: true);
        var reader = new WebSocketMessageReader();

        var status = await reader.Read(socket, CancellationToken.None);

        status.Should().Be(WebSocketReadStatus.TooBig);
    }

    [Fact]
    public async Task MessageReader_CloseFrame_IsClose()
    {
        var socket = new ScriptedSocket();
        socket.EnqueueClose();
        var reader = new WebSocketMessageReader();

        var status = await reader.Read(socket, CancellationToken.None);

        status.Should().Be(WebSocketReadStatus.Close);
    }

    [Fact]
    public async Task ConnectionReader_RoutesByKind_DropsUnreadablePayload()
    {
        var socket = new ScriptedSocket();
        socket.Enqueue(Pack(new OneWayMessageFromClient { Context = new SharedSessionPlayer.LocalUpdate { Index = 1 } }), true);
        socket.Enqueue([0xff, 0xff, 0xff], endOfMessage: true);
        socket.Enqueue(Pack(new RequestMessageFromClient { RequestId = 7, Context = new SharedSessionPlayer.LocalUpdate() }), true);
        socket.Enqueue(Pack(new ResponseMessageFromClient { RequestId = 8, Context = new EmptyResponse() }), true);
        socket.EnqueueClose();

        var reader = new ConnectionReader(socket, NullLogger.Instance);
        var lifetime = new Lifetime();
        var seen = new List<string>();
        reader.OneWay.Advise(lifetime, message => seen.Add($"oneway {((SharedSessionPlayer.LocalUpdate)message.Context).Index}"));
        reader.Requests.Advise(lifetime, message => seen.Add($"request {message.RequestId}"));
        reader.Responses.Advise(lifetime, message => seen.Add($"response {message.RequestId}"));

        await reader.Run(lifetime);

        seen.Should().Equal("oneway 1", "request 7", "response 8");
    }

    [Fact]
    public async Task ConnectionReader_TerminatedLifetime_Throws()
    {
        var reader = new ConnectionReader(new ScriptedSocket(), NullLogger.Instance);

        await FluentActions.Awaiting(() => reader.Run(new TerminatedLifetime()))
                           .Should().ThrowAsync<InvalidOperationException>();
    }

    private static byte[] Pack(IMessageFromClient message)
    {
        return MemoryPackSerializer.Serialize(message);
    }

    // Returns the queued parts one per ReceiveAsync, cut to the free space of the buffer like a real socket.
    private sealed class ScriptedSocket : WebSocket
    {
        private readonly Queue<(byte[] Bytes, bool EndOfMessage, bool IsClose)> _parts = new();
        private WebSocketState _state = WebSocketState.Open;

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

        public override void Abort()
        {
            _state = WebSocketState.Aborted;
        }

        public override void Dispose()
        {
        }
    }
}
