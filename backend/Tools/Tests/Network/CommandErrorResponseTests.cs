using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using Common.Extensions;
using Common.Network;
using Common.Reactive;
using FluentAssertions;
using Game.Session;
using MemoryPack;
using MetaGateway.UserFlow.Commands;
using MetaGateway.UserFlow.Connection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shared;
using Xunit;
using ErrorCode = Shared.ErrorCode;

namespace Tests.Network;

/// <summary>
/// N4 (port of peace-data-reborn F2.3): every request is answered over the wire. A command that throws and a request without a command are
/// answered with ErrorResponse (game session dispatcher and meta user dispatcher), the client side never hangs.
/// A real websocket pair: the server side is the real Connection (reader, writer, MemoryPack framing).
/// </summary>
public class CommandErrorResponseTests
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task Game_CommandThrows_ClientGetsErrorResponse()
    {
        await using var pair = await GamePair.Create(new ThrowingGameCommand());

        await pair.Client.SendRequest(new SharedSessionPlayer.LocalUpdate { Index = 1 }, 7);
        var response = await pair.Client.ReadResponse();

        response.RequestId.Should().Be(7);
        var error = response.Context.Should().BeOfType<ErrorResponse>().Subject;
        error.Code.Should().Be(ErrorCode.Internal);
        error.Message.Should().Contain("SharedSessionPlayer.LocalUpdate").And.Contain("command failed");
    }

    // A refusal by the session (SessionRequestException) carries its own code, not Internal.
    [Fact]
    public async Task Game_CommandRefuses_ClientGetsItsCode()
    {
        await using var pair = await GamePair.Create(new RefusingGameCommand());

        await pair.Client.SendRequest(new SharedSessionPlayer.LocalUpdate { Index = 1 }, 8);
        var response = await pair.Client.ReadResponse();

        response.RequestId.Should().Be(8);
        var error = response.Context.Should().BeOfType<ErrorResponse>().Subject;
        error.Code.Should().Be(ErrorCode.Forbidden);
        error.Message.Should().Contain("out of range");
    }

    [Fact]
    public async Task Game_UnknownCommand_ClientGetsUnknownCommand()
    {
        await using var pair = await GamePair.Create(new ThrowingGameCommand());

        await pair.Client.SendRequest(new SharedSessionService.GetRequest { Key = "x", PropertiesIds = [] }, 3);
        var response = await pair.Client.ReadResponse();

        response.RequestId.Should().Be(3);
        response.Context.Should().BeOfType<ErrorResponse>().Which.Code.Should().Be(ErrorCode.UnknownCommand);
    }

    [Fact]
    public async Task Game_OneWayThrows_SessionKeepsAnswering()
    {
        await using var pair = await GamePair.Create(new ThrowingGameCommand(), new ThrowingOneWayCommand());

        await pair.Client.SendOneWay(new SharedSessionPlayer.RemoteDisconnect { Index = 1 });
        await pair.Client.SendOneWay(new SharedSessionObject.SetProperty());
        await pair.Client.SendRequest(new SharedSessionPlayer.LocalUpdate { Index = 2 }, 9);

        var response = await pair.Client.ReadResponse();
        response.RequestId.Should().Be(9);
        response.Context.Should().BeOfType<ErrorResponse>();
    }

    [Fact]
    public async Task Meta_CommandThrows_ClientGetsErrorResponse()
    {
        await using var pair = await MetaPair.Create(new ThrowingMetaCommand());

        await pair.Client.SendRequest(new SharedSessionPlayer.LocalUpdate { Index = 1 }, 11);
        var response = await pair.Client.ReadResponse();

        response.RequestId.Should().Be(11);
        var error = response.Context.Should().BeOfType<ErrorResponse>().Subject;
        error.Code.Should().Be(ErrorCode.Internal);
        error.Message.Should().Contain("meta command failed");
    }

    [Fact]
    public async Task Meta_UnknownCommand_ClientGetsUnknownCommand()
    {
        await using var pair = await MetaPair.Create(new ThrowingMetaCommand());

        await pair.Client.SendRequest(new SharedMatchmaking.SearchLobby(), 12);
        var response = await pair.Client.ReadResponse();

        response.RequestId.Should().Be(12);
        response.Context.Should().BeOfType<ErrorResponse>().Which.Code.Should().Be(ErrorCode.UnknownCommand);
    }

    [Fact]
    public async Task Meta_CommandSucceeds_ClientGetsResult()
    {
        await using var pair = await MetaPair.Create(new OkMetaCommand());

        await pair.Client.SendRequest(new SharedSessionPlayer.LocalUpdate { Index = 5 }, 13);
        var response = await pair.Client.ReadResponse();

        response.Context.Should().BeOfType<SharedSessionPlayer.RemoteDisconnect>().Which.Index.Should().Be(5);
    }

    private class ThrowingGameCommand : ResponseCommand<SharedSessionPlayer.LocalUpdate, SharedSessionPlayer.RemoteDisconnect>
    {
        protected override SharedSessionPlayer.RemoteDisconnect Execute(IUser user, SharedSessionPlayer.LocalUpdate request)
        {
            throw new InvalidOperationException("command failed");
        }
    }

    private class RefusingGameCommand : ResponseCommand<SharedSessionPlayer.LocalUpdate, SharedSessionPlayer.RemoteDisconnect>
    {
        protected override SharedSessionPlayer.RemoteDisconnect Execute(IUser user, SharedSessionPlayer.LocalUpdate request)
        {
            throw new SessionRequestException(ErrorCode.Forbidden, "out of range");
        }
    }

    private class ThrowingOneWayCommand : Command<SharedSessionPlayer.RemoteDisconnect>
    {
        protected override void Execute(IUser user, SharedSessionPlayer.RemoteDisconnect context)
        {
            throw new InvalidOperationException("one-way failed");
        }
    }

    private class ThrowingMetaCommand : UserCommand<SharedSessionPlayer.LocalUpdate>
    {
        protected override Task<INetworkContext> Execute(IUserSession session, SharedSessionPlayer.LocalUpdate request)
        {
            throw new InvalidOperationException("meta command failed");
        }
    }

    private class OkMetaCommand : UserCommand<SharedSessionPlayer.LocalUpdate>
    {
        protected override async Task<INetworkContext> Execute(IUserSession session, SharedSessionPlayer.LocalUpdate request)
        {
            await Task.Yield();
            return new SharedSessionPlayer.RemoteDisconnect { Index = request.Index };
        }
    }

    private sealed class GamePair : IAsyncDisposable
    {
        private GamePair(SocketPair sockets, Lifetime lifetime)
        {
            _sockets = sockets;
            _lifetime = lifetime;
        }

        private readonly SocketPair _sockets;
        private readonly Lifetime _lifetime;

        public TestClient Client => _sockets.Client;

        public static async Task<GamePair> Create(IResponseCommand command, ICommand? oneWay = null)
        {
            var sockets = await SocketPair.Create();
            var lifetime = new Lifetime();
            var connection = new Connection(sockets.Server, lifetime, NullLogger.Instance);

            var user = Substitute.For<IUser>();
            user.Id.Returns(Guid.NewGuid());
            user.Index.Returns(1);
            user.Connection.Returns(connection);

            var oneWays = oneWay == null ? Array.Empty<ICommand>() : [oneWay];
            var commands = new CommandsCollection(oneWays, [command]);
            var queue = new ExecutionQueue(NullLogger<ExecutionQueue>.Instance);
            var dispatcher = new CommandDispatcher(commands, queue, NullLogger<CommandDispatcher>.Instance);

            dispatcher.Run(lifetime, user);
            connection.Run().NoAwait();

            return new GamePair(sockets, lifetime);
        }

        public async ValueTask DisposeAsync()
        {
            _lifetime.Terminate();
            await _sockets.DisposeAsync();
        }
    }

    private sealed class MetaPair : IAsyncDisposable
    {
        private MetaPair(SocketPair sockets, Lifetime lifetime)
        {
            _sockets = sockets;
            _lifetime = lifetime;
        }

        private readonly SocketPair _sockets;
        private readonly Lifetime _lifetime;

        public TestClient Client => _sockets.Client;

        public static async Task<MetaPair> Create(IUserCommand command)
        {
            var sockets = await SocketPair.Create();
            var lifetime = new Lifetime();
            var connection = new Connection(sockets.Server, lifetime, NullLogger.Instance);

            var session = new UserSession
            {
                UserId = Guid.NewGuid(),
                Connection = connection
            };

            var dispatcher = new UserCommandsDispatcher(new UserCommandsCollection([command]),
                NullLogger<UserCommandsDispatcher>.Instance);

            dispatcher.Run(session);
            connection.Run().NoAwait();

            return new MetaPair(sockets, lifetime);
        }

        public async ValueTask DisposeAsync()
        {
            _lifetime.Terminate();
            await _sockets.DisposeAsync();
        }
    }

    // A loopback TCP pair with websocket framing on both ends (no HTTP upgrade needed).
    private sealed class SocketPair : IAsyncDisposable
    {
        private SocketPair(TcpClient server, TcpClient client)
        {
            _server = server;
            _client = client;

            Server = WebSocket.CreateFromStream(server.GetStream(), true, null, TimeSpan.Zero);
            Client = new TestClient(WebSocket.CreateFromStream(client.GetStream(), false, null, TimeSpan.Zero));
        }

        private readonly TcpClient _server;
        private readonly TcpClient _client;

        public WebSocket Server { get; }
        public TestClient Client { get; }

        public static async Task<SocketPair> Create()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();

            try
            {
                var client = new TcpClient();
                var connect = client.ConnectAsync(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port);
                var server = await listener.AcceptTcpClientAsync().WaitAsync(Wait);
                await connect.WaitAsync(Wait);

                return new SocketPair(server, client);
            }
            finally
            {
                listener.Stop();
            }
        }

        public ValueTask DisposeAsync()
        {
            Server.Abort();
            Client.Socket.Abort();
            _server.Dispose();
            _client.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class TestClient(WebSocket socket)
    {
        public WebSocket Socket { get; } = socket;

        public Task SendRequest(INetworkContext context, int requestId)
        {
            return Send(new RequestMessageFromClient { Context = context, RequestId = requestId });
        }

        public Task SendOneWay(INetworkContext context)
        {
            return Send(new OneWayMessageFromClient { Context = context });
        }

        public async Task<ResponseMessageFromServer> ReadResponse()
        {
            using var timeout = new CancellationTokenSource(Wait);
            var buffer = new byte[64 * 1024];
            using var stream = new MemoryStream();

            WebSocketReceiveResult result;

            do
            {
                result = await Socket.ReceiveAsync(buffer, timeout.Token);
                stream.Write(buffer, 0, result.Count);
            }
            while (result.EndOfMessage == false);

            var message = MemoryPackSerializer.Deserialize<IMessageFromServer>(stream.ToArray());
            return message.Should().BeOfType<ResponseMessageFromServer>().Subject;
        }

        private async Task Send(IMessageFromClient message)
        {
            var bytes = MemoryPackSerializer.Serialize(message);
            await Socket.SendAsync(bytes, WebSocketMessageType.Binary, true, CancellationToken.None);
        }
    }
}
