using Framework.Tests.Fixtures;
using System.Net.WebSockets;
using Common.Network;
using Internal;
using FluentAssertions;
using MemoryPack;
using Microsoft.Extensions.Logging.Abstractions;
using Shared;
using Xunit;

namespace Framework.Tests.Network;

// F8.5: WebSocketMessageReader assembles a message from its parts and limits its size; ConnectionReader routes
// the messages by kind (a message it cannot read: ConnectionProtocolErrorTests).
public class ConnectionReaderTests : LifetimeTest
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
    public async Task ConnectionReader_RoutesByKind()
    {
        var socket = new ScriptedSocket();
        socket.Enqueue(Pack(new OneWayMessageFromClient { Context = new SharedSessionLatency.Ping { Id = 1 } }), true);
        socket.Enqueue(Pack(new RequestMessageFromClient { RequestId = 7, Context = new SharedSessionLatency.Ping() }), true);
        socket.EnqueueClose();

        var reader = new ConnectionReader(socket, NullLogger.Instance);
        var lifetime = new Lifetime(TestLifetime);
        var seen = new List<string>();
        reader.OneWay.Advise(lifetime, message => seen.Add($"oneway {((SharedSessionLatency.Ping)message.Context).Id}"));
        reader.Requests.Advise(lifetime, message => seen.Add($"request {message.RequestId}"));

        await reader.Run(lifetime);

        seen.Should().Equal("oneway 1", "request 7");
    }

    // The server has sent its close: the reader reads on to the close of the client (a socket disposed before it resets
    // the connection), and what the client sent in between is not handled any more.
    [Fact]
    public async Task ConnectionReader_CloseSent_ReadsToTheCloseOfTheClient_HandlesNothingMore()
    {
        var socket = new ScriptedSocket();
        socket.Enqueue(Pack(new OneWayMessageFromClient { Context = new SharedSessionLatency.Ping { Id = 1 } }), true);
        socket.Enqueue(Pack(new OneWayMessageFromClient { Context = new SharedSessionLatency.Ping { Id = 2 } }), true);
        socket.EnqueueClose();

        var reader = new ConnectionReader(socket, NullLogger.Instance);
        var lifetime = new Lifetime(TestLifetime);
        var seen = new List<int>();

        reader.OneWay.Advise(lifetime, message => {
            seen.Add(((SharedSessionLatency.Ping)message.Context).Id);
            socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
        });

        await reader.Run(lifetime);

        seen.Should().Equal(1);
        socket.PendingParts.Should().Be(0);
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
}
