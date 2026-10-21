using System.Net.WebSockets;
using System.Threading.Channels;
using Common.Reactive;
using FluentAssertions;
using Game.Session;
using MemoryPack;
using Microsoft.Extensions.Logging.Abstractions;
using Shared;
using Tests.Network;
using Xunit;

namespace Tests.Game;

/// <summary>
/// N2: a session never outlives its players. A match closes when an expected player does not come within the join
/// timeout, and as soon as no human is left (a bot does not hold it): after a close frame or after the keep-alive
/// aborted a half-open socket. A lobby is kept for a grace period and closes once empty.
/// </summary>
public class SessionCloseTests
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task Match_ExpectedUserNeverJoins_ClosesAfterJoinTimeout()
    {
        var world = new SessionWorld(SessionType.Match, expectedUsers: 2);
        world.ConnectUser(out _);

        var allConnected = false;
        world.Session.AllUsersConnected.Advise(world.Session.Lifetime, () => allConnected = true);
        var run = world.Session.Run();

        await WaitTerminated(world.Session.Lifetime);
        await run.WaitAsync(Wait);

        allConnected.Should().BeFalse();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MatchWithBot_HumanLeaves_SessionCloses(bool isHalfOpen)
    {
        var world = new SessionWorld(SessionType.Match, expectedUsers: 2);
        world.ConnectUser(out var socket);
        world.Session.UserFactory.CreateBot(world.Session.Lifetime, Guid.NewGuid());

        var allConnected = world.Session.AllUsersConnected.WaitInvoke(world.Session.Lifetime);
        var run = world.Session.Run();
        await allConnected.WaitAsync(Wait);

        world.Session.Lifetime.IsTerminated.Should().BeFalse();

        // Half-open: the keep-alive found no pong and aborted the socket. Otherwise the client sent a close frame.
        if (isHalfOpen == true)
            socket.Abort();
        else
            socket.ReceiveClose();

        await WaitTerminated(world.Session.Lifetime);
        await run.WaitAsync(Wait);
    }

    [Fact]
    public async Task Match_OneOfTwoHumansLeaves_SessionStaysOpen()
    {
        var world = new SessionWorld(SessionType.Match, expectedUsers: 2);
        world.ConnectUser(out var first);
        world.ConnectUser(out var second);

        var allConnected = world.Session.AllUsersConnected.WaitInvoke(world.Session.Lifetime);
        var run = world.Session.Run();
        await allConnected.WaitAsync(Wait);

        first.Abort();
        await world.WaitUsersCount(1);
        world.Session.Lifetime.IsTerminated.Should().BeFalse();

        second.ReceiveClose();
        await WaitTerminated(world.Session.Lifetime);
        await run.WaitAsync(Wait);
    }

    [Fact]
    public async Task Lobby_EmptyAfterGrace_Closes()
    {
        var world = new SessionWorld(SessionType.Lobby, expectedUsers: 0);
        var run = world.Session.Run();

        await Task.Delay(world.Data.EmptyLobbyGrace / 2);
        world.Session.Lifetime.IsTerminated.Should().BeFalse();

        await WaitTerminated(world.Session.Lifetime);
        await run.WaitAsync(Wait);
    }

    [Fact]
    public async Task Lobby_UserConnected_StaysOpenUntilLeaves()
    {
        var world = new SessionWorld(SessionType.Lobby, expectedUsers: 0);
        var run = world.Session.Run();
        world.ConnectUser(out var socket);

        await Task.Delay(world.Data.EmptyLobbyGrace * 2);
        world.Session.Lifetime.IsTerminated.Should().BeFalse();

        socket.Abort();
        await WaitTerminated(world.Session.Lifetime);
        await run.WaitAsync(Wait);
    }

    private static Task WaitTerminated(IReadOnlyLifetime lifetime)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lifetime.Listen(() => completion.TrySetResult());
        return completion.Task.WaitAsync(Wait);
    }

    // Also used by SessionRunTests.
    internal sealed class SessionWorld
    {
        public SessionWorld(SessionType type, int expectedUsers, TimeProvider? time = null, TimeSpan? joinTimeout = null)
        {
            Data = new SessionContainerData
            {
                Id = Guid.NewGuid(),
                Type = type,
                ExpectedUsers = expectedUsers,
                Lifetime = new Lifetime(),
                JoinTimeout = joinTimeout ?? TimeSpan.FromMilliseconds(300),
                EmptyLobbyGrace = TimeSpan.FromMilliseconds(300),
                Time = time ?? TimeProvider.System
            };

            _queue = new ExecutionQueue(NullLogger<ExecutionQueue>.Instance);
            _users = new SessionUsers();

            var commands = new CommandsCollection([], []);
            var userFactory = new UserFactory(_users, _queue, commands, NullLogger<UserFactory>.Instance);

            Session = new global::Game.Session.Session(Data,
                userFactory,
                _users,
                new SessionEntities(),
                _queue,
                NullLogger<global::Game.Session.Session>.Instance);
        }

        private readonly ExecutionQueue _queue;
        private readonly SessionUsers _users;

        public SessionContainerData Data { get; }
        public ISession Session { get; }

        // Same as the gateway: the user is created in the execution queue.
        public void ConnectUser(out ScriptedSocket socket)
        {
            ConnectUser(Guid.NewGuid(), out socket);
        }

        public void ConnectUser(Guid backendId, out ScriptedSocket socket)
        {
            var created = new ScriptedSocket();
            socket = created;
            _queue.Enqueue(() => Session.UserFactory.Create(Session.Lifetime, backendId, created));
        }

        // The users of the session, read in its queue.
        public async Task<List<IUser>> GetUsers()
        {
            var completion = new TaskCompletionSource<List<IUser>>(TaskCreationOptions.RunContinuationsAsynchronously);
            _queue.Enqueue(() => completion.SetResult(_users.ToList()));
            return await completion.Task.WaitAsync(Wait);
        }

        public async Task WaitUsersCount(int count)
        {
            var deadline = DateTime.UtcNow + Wait;

            while (true)
            {
                var completion = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
                _queue.Enqueue(() => completion.SetResult(_users.Count));

                if (await completion.Task.WaitAsync(Wait) == count)
                    return;

                if (DateTime.UtcNow >= deadline)
                    throw new TimeoutException($"Users count did not reach {count}");

                await Task.Delay(20);
            }
        }
    }

    // A client socket: receive waits until the client closes it or the keep-alive aborts it.
    internal sealed class ScriptedSocket : WebSocket
    {
        private readonly TaskCompletionSource<WebSocketReceiveResult> _receive =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly Channel<INetworkContext> _sent = Channel.CreateUnbounded<INetworkContext>();

        private WebSocketState _state = WebSocketState.Open;

        public override WebSocketCloseStatus? CloseStatus => null;
        public override string? CloseStatusDescription => null;
        public override WebSocketState State => _state;
        public override string? SubProtocol => null;

        public void ReceiveClose()
        {
            _state = WebSocketState.CloseReceived;
            _receive.TrySetResult(new WebSocketReceiveResult(0, WebSocketMessageType.Close, true));
        }

        // As ManagedWebSocket on a keep-alive timeout: the pending receive is cancelled.
        public override void Abort()
        {
            _state = WebSocketState.Aborted;
            _receive.TrySetException(new OperationCanceledException("The WebSocket didn't receive a Pong frame in response to a Ping frame within the configured KeepAliveTimeout."));
        }

        public override Task<WebSocketReceiveResult> ReceiveAsync(
            ArraySegment<byte> buffer,
            CancellationToken cancellationToken)
        {
            return _receive.Task.WaitAsync(cancellationToken);
        }

        public override Task SendAsync(
            ArraySegment<byte> buffer,
            WebSocketMessageType messageType,
            bool endOfMessage,
            CancellationToken cancellationToken)
        {
            var message = (OneWayMessageFromServer)MemoryPackSerializer.Deserialize<IMessageFromServer>(buffer.AsSpan())!;
            _sent.Writer.TryWrite(message.Context);
            return Task.CompletedTask;
        }

        // Waits for the next one-way context the session sent to this client.
        public async Task<INetworkContext> ReadSent(TimeSpan timeout)
        {
            return await _sent.Reader.ReadAsync().AsTask().WaitAsync(timeout);
        }

        // The contexts sent so far, without waiting.
        public List<INetworkContext> DrainSent()
        {
            var result = new List<INetworkContext>();

            while (_sent.Reader.TryRead(out var context) == true)
                result.Add(context);

            return result;
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
        }
    }
}
