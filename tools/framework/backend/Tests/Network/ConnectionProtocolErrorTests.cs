using Framework.Tests.Fixtures;
using System.Net.WebSockets;
using Common.Network;
using FluentAssertions;
using Internal;
using MemoryPack;
using Microsoft.Extensions.Logging.Abstractions;
using Shared;
using Xunit;

namespace Framework.Tests.Network;

// A message the server cannot take ends the connection with a close status instead of being dropped: a dropped request
// would leave the client waiting for a response that never comes.
public class ConnectionProtocolErrorTests : LifetimeTest
{
    [Fact]
    public async Task Reader_UnreadablePayload_ClosesWithInvalidPayload()
    {
        var socket = new ScriptedSocket();
        // An unknown union tag of IMessageFromClient.
        socket.Enqueue([0x07, 0x00, 0x00], endOfMessage: true);
        socket.Enqueue(Pack(new RequestMessageFromClient { RequestId = 7, Context = new SharedSessionLatency.Ping() }), true);
        socket.EnqueueClose();
        var reader = new ConnectionReader(socket, NullLogger.Instance);
        var lifetime = new Lifetime(TestLifetime);
        var seen = new List<int>();
        reader.Requests.Advise(lifetime, message => seen.Add(message.RequestId));

        await reader.Run(lifetime);

        socket.SentCloseStatus.Should().Be(WebSocketCloseStatus.InvalidPayloadData);
        seen.Should().BeEmpty();
    }

    // The server sends no requests, so a response of the client answers nothing.
    [Fact]
    public async Task Reader_ResponseFromClient_ClosesWithProtocolError()
    {
        var socket = new ScriptedSocket();
        socket.Enqueue(Pack(new ResponseMessageFromClient { RequestId = 8, Context = new EmptyResponse() }), true);
        socket.Enqueue(Pack(new RequestMessageFromClient { RequestId = 7, Context = new SharedSessionLatency.Ping() }), true);
        socket.EnqueueClose();
        var reader = new ConnectionReader(socket, NullLogger.Instance);
        var lifetime = new Lifetime(TestLifetime);
        var seen = new List<int>();
        reader.Requests.Advise(lifetime, message => seen.Add(message.RequestId));

        await reader.Run(lifetime);

        socket.SentCloseStatus.Should().Be(WebSocketCloseStatus.ProtocolError);
        seen.Should().BeEmpty();
    }

    // The handshake comes before the ticket is checked: a socket nobody trusts yet must not hold a buffer of the size an
    // authorized connection may use.
    [Fact]
    public async Task Handshake_FrameOverHandshakeLimit_IsTooBig()
    {
        var socket = new ScriptedSocket();
        socket.Enqueue(new byte[ConnectionOneTimeHandle.MaxRequestBytes + 1], endOfMessage: true);
        using var handle = new ConnectionOneTimeHandle(socket);

        var read = () => handle.ReadRequest<SharedSessionAuth.Request>(CancellationToken.None);

        await read.Should().ThrowAsync<InvalidOperationException>().WithMessage($"*{WebSocketReadStatus.TooBig}*");
    }

    private static byte[] Pack(IMessageFromClient message)
    {
        return MemoryPackSerializer.Serialize(message);
    }
}
