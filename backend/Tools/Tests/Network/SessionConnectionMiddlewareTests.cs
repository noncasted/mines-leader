using System.Net.WebSockets;
using Common.Network;
using Common.Reactive;
using FluentAssertions;
using Game.Global;
using Game.Session;
using GameGateway;
using Infrastructure.Startup;
using MemoryPack;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shared;
using Tests.Game;
using Tests.Security;
using Xunit;

namespace Tests.Network;

/// <summary>
/// S4: the game gateway takes the user and the session only from a ticket and a live place of that user. Every
/// refusal is explicit: the client gets the status in the auth response and a close, not a dropped socket. Before S4
/// the gateway believed the user and the session ids the client wrote into the first frame.
/// </summary>
public class SessionConnectionMiddlewareTests
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task UnknownSession_RespondsClosedAndCloses()
    {
        var world = new World();
        await using var host = await world.Start();
        using var socket = await Connect(host);

        await Send(socket, new SharedSessionAuth.Request { Ticket = world.Tickets.Issue(Guid.NewGuid(), Guid.NewGuid()) });

        await ExpectRefusal(socket, SessionAuthStatus.Closed);
    }

    // The first frame of a client before S4 (or of one that makes the ids up): no ticket, no entry.
    [Fact]
    public async Task NoTicket_RespondsInvalidTicketAndCloses()
    {
        var world = new World();
        world.AddSession(Guid.NewGuid());
        await using var host = await world.Start();
        using var socket = await Connect(host);

        await Send(socket, new SharedSessionAuth.Request());

        await ExpectRefusal(socket, SessionAuthStatus.InvalidTicket);
    }

    // A ticket of the meta for one user, used for the place of another: the gateway takes the user from the ticket.
    [Fact]
    public async Task TicketOfAnotherUser_RespondsNoReservationAndCloses()
    {
        var world = new World();
        var session = world.AddSession(Guid.NewGuid());
        await using var host = await world.Start();
        using var socket = await Connect(host);

        await Send(socket, new SharedSessionAuth.Request { Ticket = world.Tickets.Issue(Guid.NewGuid(), session.Id) });

        await ExpectRefusal(socket, SessionAuthStatus.NoReservation);
    }

    [Fact]
    public async Task ValidTicket_RespondsOk_ThenTheSessionTalks()
    {
        var world = new World();
        var userId = Guid.NewGuid();
        var session = world.AddSession(userId);
        await using var host = await world.Start();
        using var socket = await Connect(host);

        await Send(socket, new SharedSessionAuth.Request { Ticket = world.Tickets.Issue(userId, session.Id) });

        var reader = new WebSocketMessageReader();
        using var cancellation = new CancellationTokenSource(Wait);
        (await reader.Read(socket, cancellation.Token)).Should().Be(WebSocketReadStatus.Message);

        var response = MemoryPackSerializer.Deserialize<IMessageFromServer>(reader.Payload.Span)
                                           .Should().BeOfType<ResponseMessageFromServer>().Subject;

        response.Context.Should().BeOfType<SharedSessionAuth.Response>()
                .Which.Status.Should().Be(SessionAuthStatus.Ok);

        await world.WaitUsersCount(1);
        (await world.GetUsers()).Single().BackendId.Should().Be(userId);

        // The session is not run here: nothing reads the socket, the end of the session releases the connection.
        socket.Abort();
        world.Terminate();
    }

    private static async Task ExpectRefusal(ClientWebSocket socket, SessionAuthStatus status)
    {
        using var cancellation = new CancellationTokenSource(Wait);
        var reader = new WebSocketMessageReader();
        (await reader.Read(socket, cancellation.Token)).Should().Be(WebSocketReadStatus.Message);

        var message = MemoryPackSerializer.Deserialize<IMessageFromServer>(reader.Payload.Span);
        var response = message.Should().BeOfType<ResponseMessageFromServer>().Subject;
        response.RequestId.Should().Be(1);
        response.Context.Should().BeOfType<SharedSessionAuth.Response>().Which.Status.Should().Be(status);

        (await reader.Read(socket, cancellation.Token)).Should().Be(WebSocketReadStatus.Close);
        socket.CloseStatus.Should().Be(WebSocketCloseStatus.PolicyViolation);
    }

    private sealed class World
    {
        public World()
        {
            Collection.TryGet(Arg.Any<Guid>(), out Arg.Any<ISession>()).Returns(call => {
                var found = _sessions.TryGetValue(call.ArgAt<Guid>(0), out var session);
                call[1] = session;
                return found;
            });
        }

        private readonly Dictionary<Guid, ISession> _sessions = new();
        private SessionCloseTests.SessionWorld? _world;

        public SessionTickets Tickets { get; } = TestTickets.Create();
        public ISessionsCollection Collection { get; } = Substitute.For<ISessionsCollection>();

        // A match session with a place reserved for the user. One per world.
        public ISession AddSession(Guid reservedUser)
        {
            _world = new SessionCloseTests.SessionWorld(SessionType.Match, 2, joinTimeout: Wait);
            _world.Session.Reserve(reservedUser);
            _sessions.Add(_world.Session.Id, _world.Session);
            return _world.Session;
        }

        public void Terminate()
        {
            _world!.Data.Lifetime.Terminate();
        }

        public Task WaitUsersCount(int count)
        {
            return _world!.WaitUsersCount(count);
        }

        public Task<List<IUser>> GetUsers()
        {
            return _world!.GetUsers();
        }

        public async Task<TestWebHost> Start()
        {
            var participant = Substitute.For<IClusterParticipantContext>();
            participant.IsInitialized.Returns(new ViewableProperty<bool>(true));

            var builder = TestWebHost.CreateBuilder("Production");
            builder.Services.AddSingleton(Collection);
            builder.Services.AddSingleton(participant);
            builder.Services.AddSingleton<ISessionTicketVerifier>(Tickets);
            builder.Services.AddSingleton<SessionGate>();

            var app = builder.Build();
            app.UseWebSockets();
            app.UseMiddleware<SessionConnectionMiddleware>();

            return await TestWebHost.Start(app);
        }
    }

    private static async Task<ClientWebSocket> Connect(TestWebHost host)
    {
        var socket = new ClientWebSocket();
        var uri = new UriBuilder(host.Address)
        {
            Scheme = "ws",
            Path = "/",
            Query = $"{ProtocolVersion.QueryKey}={ProtocolVersion.Hash}"
        }.Uri;

        await socket.ConnectAsync(uri, CancellationToken.None);
        return socket;
    }

    private static Task Send(ClientWebSocket socket, INetworkContext context)
    {
        IMessageFromClient message = new RequestMessageFromClient
        {
            Context = context,
            RequestId = 1
        };

        var bytes = MemoryPackSerializer.Serialize(message);
        return socket.SendAsync(bytes, WebSocketMessageType.Binary, true, CancellationToken.None);
    }
}
