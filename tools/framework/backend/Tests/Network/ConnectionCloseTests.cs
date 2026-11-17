using System.Net.WebSockets;
using Common.Network;
using FluentAssertions;
using Framework.Tests.Fixtures;
using Framework.Tests.Security;
using Internal;
using MemoryPack;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Shared;
using Xunit;

namespace Framework.Tests.Network;

// A Connection over a real Kestrel socket: a forced disconnect sends a close frame, and a failure of the read loop
// still ends the connection.
public class ConnectionCloseTests
{
    // No send and receive buffers of a loopback socket hold it: a send of it waits for the client to read.
    private const int LargerThanTheSocketBuffers = 64 * 1024 * 1024;

    [Fact]
    public async Task ForceDisconnect_ClientGetsCloseFrame()
    {
        await using var server = await ConnectionServer.Start();
        using var client = await server.Connect();
        var connection = await server.Accepted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        connection.ForceDisconnect();

        var result = await client.ReceiveAsync(new byte[64], TestContext.Current.CancellationToken)
                                 .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        result.MessageType.Should().Be(WebSocketMessageType.Close);
        result.CloseStatus.Should().Be(WebSocketCloseStatus.NormalClosure);
    }

    // A forced disconnect that meets the start of the Run, under a handler that ends with the Run as a gateway does:
    // whichever goes first, the client gets the close frame, not a reset of the socket.
    [Theory]
    [InlineData(ForceDisconnectMoment.BeforeRun)]
    [InlineData(ForceDisconnectMoment.WithRun)]
    public async Task ForceDisconnect_AtTheStartOfRun_ClientGetsCloseFrame(ForceDisconnectMoment moment)
    {
        await using var server = await ConnectionServer.Start(forceDisconnect: moment);

        for (var attempt = 0; attempt < 300; attempt++)
        {
            using var client = await server.Connect();

            var result = await client.ReceiveAsync(new byte[64], TestContext.Current.CancellationToken)
                                     .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            result.MessageType.Should().Be(WebSocketMessageType.Close, $"attempt {attempt}");
            result.CloseStatus.Should().Be(WebSocketCloseStatus.NormalClosure);
        }
    }

    // The order the race above rarely hits: the disconnect ends right before the first receive of the Run, the read loop
    // is past its checks. A cancelled token aborts the socket in ReceiveAsync, the close frame may die with it.
    [Fact]
    public async Task ForceDisconnect_RightBeforeTheFirstReceive_TheReceiveIsNotCancelled()
    {
        await using var server = await ConnectionServer.Start(disconnectBeforeFirstReceive: true);
        using var client = await server.Connect();

        var result = await client.ReceiveAsync(new byte[64], TestContext.Current.CancellationToken)
                                 .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        result.MessageType.Should().Be(WebSocketMessageType.Close);
        (await server.FirstReceiveCancelled.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken))
            .Should().BeFalse("the receive of a connection in its close waits for the answer of the client");
    }

    [Fact]
    public async Task Run_HandlerThrows_ConnectionEnds()
    {
        await using var server = await ConnectionServer.Start(connection => {
            connection.Reader.OneWay.Advise(connection.Lifetime, _ => throw new InvalidOperationException("handler bug"));
        });

        using var client = await server.Connect();
        var connection = await server.Accepted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        var message = MemoryPackSerializer.Serialize<IMessageFromClient>(
            new OneWayMessageFromClient { Context = new SharedSessionLatency.Ping { Id = 1 } });

        await client.SendAsync(message, WebSocketMessageType.Binary, true, TestContext.Current.CancellationToken);

        var run = await server.Finished.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        run.Should().BeOfType<InvalidOperationException>();
        connection.Lifetime.IsTerminated.Should().BeTrue("a connection whose read loop failed is over");

        var result = await client.ReceiveAsync(new byte[64], TestContext.Current.CancellationToken)
                                 .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        result.MessageType.Should().Be(WebSocketMessageType.Close);
    }

    // Run is what a gateway keeps the socket for: it ends after the close frame of a failed read loop went out, however
    // long the socket takes to send it.
    [Fact]
    public async Task Run_HandlerThrows_TheSocketLivesUntilTheCloseFrameWentOut()
    {
        var hold = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var server = await ConnectionServer.Start(
            connection => connection.Reader.OneWay.Advise(connection.Lifetime, _ => throw new InvalidOperationException("handler bug")),
            holdClose: hold.Task);

        using var client = await server.Connect();
        await server.Accepted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        var message = MemoryPackSerializer.Serialize<IMessageFromClient>(
            new OneWayMessageFromClient { Context = new SharedSessionLatency.Ping { Id = 1 } });

        await client.SendAsync(message, WebSocketMessageType.Binary, true, TestContext.Current.CancellationToken);

        await server.CloseStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        // What the handler of the old order did right away; on the right order nothing can happen here, whatever the load.
        await Task.WhenAny(server.SocketDisposed.Task, Task.Delay(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken));
        hold.SetResult();

        await AssertCloseFrame(client);
    }

    // The close of a client: the answer goes out through the same close.
    [Fact]
    public async Task Run_ClientCloses_TheSocketLivesUntilTheAnswerWentOut()
    {
        var hold = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var server = await ConnectionServer.Start(holdClose: hold.Task);
        using var client = await server.Connect();
        await server.Accepted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        await client.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "bye", TestContext.Current.CancellationToken);
        await server.CloseStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        await Task.WhenAny(server.SocketDisposed.Task, Task.Delay(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken));
        hold.SetResult();

        await AssertCloseFrame(client);
    }

    // The session of the connection is over before its Run: the client still gets the close, not a reset, and the Run ends
    // as an ordinary disconnect.
    [Fact]
    public async Task Run_AfterTheEndOfTheParent_ClientGetsCloseFrame()
    {
        await using var server = await ConnectionServer.Start(terminatedParent: true);
        using var client = await server.Connect();

        await AssertCloseFrame(client);

        (await server.Finished.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken))
            .Should().BeNull();
    }

    // The session of the connection ends while the read loop waits for the client: the client gets the close, not a reset
    // of the socket, and the Run ends after it.
    [Fact]
    public async Task Run_TheParentEndsDuringTheRun_ClientGetsCloseFrame()
    {
        var parent = new Lifetime();

        await using var server = await ConnectionServer.Start(parent: parent);
        using var client = await server.Connect();
        var connection = await server.Accepted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        parent.Terminate();

        connection.Lifetime.IsTerminated.Should().BeTrue();
        await AssertCloseFrame(client);

        await client.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "bye", TestContext.Current.CancellationToken);

        (await server.Finished.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken))
            .Should().BeNull();
    }

    // A send in flight when the connection ends: the client that reads on gets the message and then the close frame. A
    // cancellation of the send aborts the socket (ManagedWebSocket) and takes both with it.
    [Theory]
    [InlineData(ConnectionEnd.ForceDisconnect)]
    [InlineData(ConnectionEnd.Parent)]
    public async Task End_SendInFlight_ClientGetsTheMessageAndTheCloseFrame(ConnectionEnd end)
    {
        var parent = new Lifetime();

        await using var server = await ConnectionServer.Start(parent: parent);
        using var client = await server.Connect();
        var connection = await server.Accepted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        connection.Writer.WriteOneWay(new SharedSessionPeer.Tunnel { Frame = new byte[LargerThanTheSocketBuffers] });
        await TestWait.Until(() => server.IsSendInFlight);

        if (end == ConnectionEnd.ForceDisconnect)
            connection.ForceDisconnect();
        else
            parent.Terminate();

        var buffer = new byte[1024 * 1024];
        var received = 0L;
        WebSocketReceiveResult result;

        do
        {
            result = await client.ReceiveAsync(buffer, TestContext.Current.CancellationToken)
                                 .WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            received += result.Count;
        }
        while (result.MessageType != WebSocketMessageType.Close);

        result.CloseStatus.Should().Be(WebSocketCloseStatus.NormalClosure);
        received.Should().BeGreaterThan(LargerThanTheSocketBuffers);
    }

    // A client that does not read holds the send and the close frame behind it: the close does not wait for it forever.
    [Fact]
    public async Task ForceDisconnect_SendStuckOnAClientThatDoesNotRead_RunEnds()
    {
        await using var server = await ConnectionServer.Start(closeTimeoutReachesTheSocket: true);
        using var client = await server.Connect();
        var connection = await server.Accepted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        connection.Writer.WriteOneWay(new SharedSessionPeer.Tunnel { Frame = new byte[LargerThanTheSocketBuffers] });
        await TestWait.Until(() => server.IsSendInFlight);

        connection.ForceDisconnect();

        await server.Finished.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        await TestWait.Until(() => server.IsSendInFlight == false);
    }

    // The read loop is over, so no cancelled receive aborts the socket, and the handler keeps it after the Run: the send
    // ends with the close all the same.
    [Fact]
    public async Task Run_HandlerThrows_SendStuckOnAClientThatDoesNotRead_TheSendEndsWithTheClose()
    {
        var hold = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var server = await ConnectionServer.Start(
            connection => connection.Reader.OneWay.Advise(connection.Lifetime, _ => throw new InvalidOperationException("handler bug")),
            closeTimeoutReachesTheSocket: true,
            holdSocket: hold.Task);

        using var client = await server.Connect();
        var connection = await server.Accepted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        connection.Writer.WriteOneWay(new SharedSessionPeer.Tunnel { Frame = new byte[LargerThanTheSocketBuffers] });
        await TestWait.Until(() => server.IsSendInFlight);

        var message = MemoryPackSerializer.Serialize<IMessageFromClient>(
            new OneWayMessageFromClient { Context = new SharedSessionLatency.Ping { Id = 1 } });

        await client.SendAsync(message, WebSocketMessageType.Binary, true, TestContext.Current.CancellationToken);

        await server.Finished.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        await TestWait.Until(() => server.IsSendInFlight == false);

        hold.SetResult();
    }

    private static async Task AssertCloseFrame(ClientWebSocket client)
    {
        var result = await client.ReceiveAsync(new byte[64], TestContext.Current.CancellationToken)
                                 .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        result.MessageType.Should().Be(WebSocketMessageType.Close);
        result.CloseStatus.Should().Be(WebSocketCloseStatus.NormalClosure);
    }

    public enum ConnectionEnd
    {
        ForceDisconnect,
        Parent
    }

    public enum ForceDisconnectMoment
    {
        BeforeRun,
        WithRun
    }

    private sealed class ConnectionServer : IAsyncDisposable
    {
        private ConnectionServer(TestWebHost host)
        {
            _host = host;
        }

        private readonly TestWebHost _host;

        private Task? _send;

        public TaskCompletionSource<IConnection> Accepted { get; } = new();

        public TaskCompletionSource<bool> FirstReceiveCancelled { get; } = new();

        // Completes with the exception of Connection.Run, null when it returned.
        public TaskCompletionSource<Exception?> Finished { get; } = new();

        public TaskCompletionSource CloseStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource SocketDisposed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool IsSendInFlight => _send is { IsCompleted: false };

        public void OnSend(Task send)
        {
            _send = send;
        }

        public static async Task<ConnectionServer> Start(
            Action<IConnection>? setup = null,
            ForceDisconnectMoment? forceDisconnect = null,
            bool disconnectBeforeFirstReceive = false,
            Task? holdClose = null,
            bool terminatedParent = false,
            Lifetime? parent = null,
            bool closeTimeoutReachesTheSocket = false,
            Task? holdSocket = null)
        {
            var builder = TestWebHost.CreateBuilder("Development");
            var app = builder.Build();
            app.UseWebSockets();

            ConnectionServer? server = null;

            app.Use(async (HttpContext context, RequestDelegate next) => {
                if (context.WebSockets.IsWebSocketRequest == false)
                {
                    await next(context);
                    return;
                }

                using var socket = await context.WebSockets.AcceptWebSocketAsync();
                var hooked = new HookedSocket(socket, server!, closeTimeoutReachesTheSocket);
                var connectionParent = parent ?? new Lifetime();

                var connection = new Connection(hooked, connectionParent, NullLogger.Instance);

                if (holdClose != null)
                    hooked.HoldClose(holdClose);

                if (terminatedParent == true)
                    connectionParent.Terminate();

                if (disconnectBeforeFirstReceive == true)
                {
                    hooked.RunBeforeFirstReceive(cancellation => {
                        connection.ForceDisconnect();

                        // The end of the close (it cancels the receive), or its wait for the client.
                        SpinWait.SpinUntil(() => cancellation.IsCancellationRequested, TimeSpan.FromMilliseconds(300));
                        server!.FirstReceiveCancelled.SetResult(cancellation.IsCancellationRequested);
                    });
                }

                setup?.Invoke(connection);

                // Run reads only with a handler (the session advises its dispatcher before it).
                connection.Reader.Requests.Advise(connection.Lifetime, _ => { });

                if (forceDisconnect != null)
                {
                    await RunWithForceDisconnect(connection, forceDisconnect.Value);
                    return;
                }

                try
                {
                    // The read loop is running when the test gets the connection.
                    var run = connection.Run();
                    server!.Accepted.SetResult(connection);
                    await run;
                    server!.Finished.SetResult(null);
                }
                catch (Exception e)
                {
                    server!.Finished.SetResult(e);
                }

                if (holdSocket != null)
                    await holdSocket;
            });

            server = new ConnectionServer(await TestWebHost.Start(app));
            return server;
        }

        private static async Task RunWithForceDisconnect(IConnection connection, ForceDisconnectMoment moment)
        {
            if (moment == ForceDisconnectMoment.BeforeRun)
                connection.ForceDisconnect();

            var start = new Barrier(2);

            var disconnect = moment == ForceDisconnectMoment.WithRun
                ? Task.Run(() => {
                    start.SignalAndWait();
                    connection.ForceDisconnect();
                })
                : Task.CompletedTask;

            if (moment == ForceDisconnectMoment.WithRun)
                start.SignalAndWait();

            // A Run after the disconnect completes as the disconnect: what the client got is the point here.
            await connection.Run();
            await disconnect;
        }

        public async Task<ClientWebSocket> Connect()
        {
            var client = new ClientWebSocket();
            var uri = new UriBuilder(_host.Address) { Scheme = "ws" }.Uri;
            await client.ConnectAsync(uri, TestContext.Current.CancellationToken);
            return client;
        }

        public ValueTask DisposeAsync()
        {
            return _host.DisposeAsync();
        }
    }

    // A real socket with a moment to step in right before the first receive and a close frame that goes out late.
    private sealed class HookedSocket(WebSocket socket, ConnectionServer server, bool closeTimeoutReachesTheSocket) : WebSocket
    {
        private Action<CancellationToken>? _beforeFirstReceive;
        private Task _holdClose = Task.CompletedTask;

        public void HoldClose(Task hold)
        {
            _holdClose = hold;
        }

        public void RunBeforeFirstReceive(Action<CancellationToken> hook)
        {
            _beforeFirstReceive = hook;
        }

        public override WebSocketCloseStatus? CloseStatus => socket.CloseStatus;
        public override string? CloseStatusDescription => socket.CloseStatusDescription;
        public override WebSocketState State => socket.State;
        public override string? SubProtocol => socket.SubProtocol;

        public override ValueTask<ValueWebSocketReceiveResult> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            var hook = _beforeFirstReceive;
            _beforeFirstReceive = null;
            hook?.Invoke(cancellationToken);

            return socket.ReceiveAsync(buffer, cancellationToken);
        }

        public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
        {
            throw new NotSupportedException("The connection reads through the Memory overload");
        }

        public override ValueTask SendAsync(ReadOnlyMemory<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
        {
            var send = socket.SendAsync(buffer, messageType, endOfMessage, cancellationToken).AsTask();
            server.OnSend(send);
            return new ValueTask(send);
        }

        public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
        {
            return socket.SendAsync(buffer, messageType, endOfMessage, cancellationToken);
        }

        public override async Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
        {
            server.CloseStarted.TrySetResult();
            await _holdClose;

            // Past the hold the time of the test is not the time of the socket: the timeout of the close is not the point
            // of a test, unless it asks for it.
            await socket.CloseOutputAsync(closeStatus, statusDescription, closeTimeoutReachesTheSocket ? cancellationToken : CancellationToken.None);
        }

        public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
        {
            return socket.CloseAsync(closeStatus, statusDescription, cancellationToken);
        }

        public override void Abort()
        {
            socket.Abort();
        }

        public override void Dispose()
        {
            socket.Dispose();
            server.SocketDisposed.TrySetResult();
        }
    }
}
