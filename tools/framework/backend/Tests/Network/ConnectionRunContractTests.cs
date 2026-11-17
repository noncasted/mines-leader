using Common.Network;
using FluentAssertions;
using Framework.Tests.Fixtures;
using Internal;
using MemoryPack;
using Microsoft.Extensions.Logging.Abstractions;
using Shared;
using Xunit;

namespace Framework.Tests.Network;

// The contract of the owner of a socket: the handlers are advised before Run (a message read before them is lost), and
// the owner keeps the socket until Closed (a socket that ends with its handler takes the close frame with it).
public class ConnectionRunContractTests : LifetimeTest
{
    [Fact]
    public async Task Run_WithoutHandlers_Throws()
    {
        var socket = new ScriptedSocket();
        socket.Enqueue(Pack(new RequestMessageFromClient { RequestId = 7, Context = new SharedSessionLatency.Ping() }), true);
        socket.EnqueueClose();
        var lifetime = new Lifetime(TestLifetime);
        var connection = new Connection(socket, lifetime, NullLogger.Instance);

        await FluentActions.Awaiting(() => connection.Run()).Should().ThrowAsync<InvalidOperationException>();
    }

    // A connection ended before its run (forced by the session, the end of the parent) is an ordinary disconnect.
    [Fact]
    public async Task Run_LifetimeEndedBefore_CompletesAfterTheClose()
    {
        var socket = new ScriptedSocket();
        var lifetime = new Lifetime(TestLifetime);
        var connection = new Connection(socket, lifetime, NullLogger.Instance);
        lifetime.Terminate();

        await connection.Run().WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        connection.Closed.IsCompleted.Should().BeTrue();
    }

    // The session ends before it runs the connection (the join timeout of a match): the owner awaiting Closed is not left
    // hanging, and the client gets the close frame.
    [Fact]
    public async Task ParentEndsWithoutRun_ClosedCompletes_CloseFrameSent()
    {
        var socket = new ScriptedSocket();
        var lifetime = new Lifetime(TestLifetime);
        var connection = new Connection(socket, lifetime, NullLogger.Instance);

        lifetime.Terminate();

        await connection.Closed.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        socket.SentCloseStatus.Should().Be(System.Net.WebSockets.WebSocketCloseStatus.NormalClosure);
    }

    [Fact]
    public async Task Closed_CompletesOnceTheCloseIsOver()
    {
        var socket = new ScriptedSocket();
        socket.EnqueueClose();
        var lifetime = new Lifetime(TestLifetime);
        var connection = new Connection(socket, lifetime, NullLogger.Instance);
        connection.Reader.Requests.Advise(lifetime, _ => { });

        connection.Closed.IsCompleted.Should().BeFalse();

        await connection.Run().WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        connection.Closed.IsCompletedSuccessfully.Should().BeTrue();
        socket.SentCloseStatus.Should().NotBeNull();
    }

    private static byte[] Pack(IMessageFromClient message)
    {
        return MemoryPackSerializer.Serialize(message);
    }
}
